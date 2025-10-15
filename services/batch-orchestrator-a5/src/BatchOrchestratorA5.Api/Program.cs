using System.Linq;
using System.Collections.Concurrent;
using System.Net.Http;
using Microsoft.OpenApi.Models;
using Platform.Api;
using Platform.Api.Validation;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using BatchOrchestratorA5.Api.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithEnvironmentName()
        .WriteTo.Console());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Batch Orchestrator A5 API",
        Version = "v1",
        Description = "Batch scheduling, sharding, and retry orchestration for summarization lanes."
    });
});
builder.Services.AddHealthChecks();

builder.Services.AddPlatformSecurity(builder.Configuration, options =>
{
    options.AddPolicy("BatchWriters", policy =>
        policy.RequireRole("admin", "svc.gateway", "svc.scheduler"));
});

builder.Services.AddSingleton<BatchScheduler>();
builder.Services.AddProblemDetails();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation());

builder.Services.AddHttpClient("lane-dispatcher")
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

var app = builder.Build();

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await ErrorResults.InternalServerError(context).ExecuteAsync(context);
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/healthz");

app.MapPost("/api/batch/jobs", (BatchJobRequest request, BatchScheduler scheduler, HttpContext context) =>
    {
        var idempotencyKey = context.Request.Headers["Idempotency-Key"].FirstOrDefault();
        var response = scheduler.CreateOrGet(request, idempotencyKey);
        return Results.Created($"/api/batch/jobs/{response.JobId}", response);
    })
    .AddEndpointFilter(new ValidationFilter<BatchJobRequest>())
    .Produces<BatchJobResponse>(StatusCodes.Status201Created)
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
    .RequireAuthorization("BatchWriters")
    .WithName("CreateBatchJob");

app.MapGet("/api/batch/jobs/{jobId:guid}", (Guid jobId, BatchScheduler scheduler) =>
    scheduler.TryGet(jobId, out var job)
        ? Results.Ok(job)
        : Results.NotFound(new ErrorResponse("not_found", "Batch job not found.")))
    .Produces<BatchJobResponse>()
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
    .RequireAuthorization();

app.MapGet("/api/batch/jobs/{jobId:guid}/status", (Guid jobId, BatchScheduler scheduler) =>
    scheduler.TryGetStatus(jobId, out var status)
        ? Results.Ok(status)
        : Results.NotFound(new ErrorResponse("not_found", "Batch job status not found.")))
    .Produces<BatchJobStatusResponse>()
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
    .RequireAuthorization();

app.MapPost("/api/batch/jobs/{jobId:guid}/cancel", (Guid jobId, BatchJobCancelRequest request, BatchScheduler scheduler) =>
    {
        var result = scheduler.Cancel(jobId, request.Reason);
        return result is null
            ? Results.NotFound(new ErrorResponse("not_found", "Batch job not found."))
            : Results.Ok(result);
    })
    .AddEndpointFilter(new ValidationFilter<BatchJobCancelRequest>())
    .Produces<BatchJobResponse>()
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
    .RequireAuthorization("BatchWriters");

app.MapGet("/api/batch/jobs", (BatchScheduler scheduler) => Results.Ok(scheduler.List()))
    .Produces<IReadOnlyCollection<BatchJobResponse>>()
    .RequireAuthorization("BatchWriters");

app.Run();

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() => HttpPolicyExtensions
    .HandleTransientHttpError()
    .OrResult(response => (int)response.StatusCode == StatusCodes.Status429TooManyRequests)
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() => HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

public sealed class BatchScheduler
{
    private readonly ConcurrentDictionary<Guid, BatchJobState> _jobs = new();
    private readonly ConcurrentDictionary<string, Guid> _idempotency = new(StringComparer.OrdinalIgnoreCase);

    public BatchJobResponse CreateOrGet(BatchJobRequest request, string? idempotencyKey)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey) &&
            _idempotency.TryGetValue(idempotencyKey!, out var existingJobId) &&
            _jobs.TryGetValue(existingJobId, out var existingJob))
        {
            return existingJob.ToResponse();
        }

        var job = BatchJobState.Create(request);
        _jobs[job.JobId] = job;

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            _idempotency.TryAdd(idempotencyKey!, job.JobId);
        }

        return job.ToResponse();
    }

    public bool TryGet(Guid jobId, out BatchJobResponse response)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            response = job.ToResponse();
            return true;
        }

        response = default!;
        return false;
    }

    public bool TryGetStatus(Guid jobId, out BatchJobStatusResponse status)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            status = job.ToStatusResponse();
            return true;
        }

        status = default!;
        return false;
    }

    public BatchJobResponse? Cancel(Guid jobId, string reason)
    {
        if (!_jobs.TryGetValue(jobId, out var job) || job.Status is BatchJobStatus.Completed or BatchJobStatus.Cancelled)
        {
            return null;
        }

        var cancelled = job with
        {
            Status = BatchJobStatus.Cancelled,
            FailureReason = reason,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _jobs[jobId] = cancelled;
        return cancelled.ToResponse();
    }

    public IReadOnlyCollection<BatchJobResponse> List() =>
        _jobs.Values.Select(job => job.ToResponse()).OrderByDescending(job => job.CreatedAt).ToArray();
}

public sealed record BatchJobState
{
    public Guid JobId { get; init; } = Guid.NewGuid();
    public string JobName { get; init; } = string.Empty;
    public IReadOnlyList<string> DocumentIds { get; init; } = Array.Empty<string>();
    public string Lane { get; init; } = string.Empty;
    public BatchJobStatus Status { get; init; } = BatchJobStatus.Pending;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ScheduledFor { get; init; }
    public int Priority { get; init; } = 5;
    public bool EnablePiiRedaction { get; init; } = true;
    public string? FailureReason { get; init; }
    public int CompletedCount { get; init; }
    public int FailedCount { get; init; }

    public static BatchJobState Create(BatchJobRequest request)
    {
        var initialStatus = request.ScheduledFor.HasValue && request.ScheduledFor > DateTimeOffset.UtcNow
            ? BatchJobStatus.Scheduled
            : BatchJobStatus.Pending;

        return new BatchJobState
        {
            JobName = request.JobName,
            DocumentIds = request.DeduplicateDocuments
                ? request.DocumentIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                : request.DocumentIds.ToArray(),
            Lane = request.Lane,
            Status = initialStatus,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            ScheduledFor = request.ScheduledFor,
            Priority = request.Priority,
            EnablePiiRedaction = request.EnablePiiRedaction
        };
    }

    public BatchJobResponse ToResponse() => new(
        JobId,
        JobName,
        DocumentIds,
        Lane,
        Status,
        CreatedAt,
        ScheduledFor,
        Priority,
        EnablePiiRedaction,
        FailureReason
    );

    public BatchJobStatusResponse ToStatusResponse() => new(
        JobId,
        Status,
        UpdatedAt,
        CompletedCount,
        FailedCount
    );
}

public partial class Program;
