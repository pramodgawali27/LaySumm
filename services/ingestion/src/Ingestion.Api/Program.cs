using System.Linq;
using System.Collections.Concurrent;
using System.Net.Http;
using Ingestion.Api.Contracts;
using Microsoft.OpenApi.Models;
using Platform.Api;
using Platform.Api.Validation;
using Polly;
using Polly.Extensions.Http;
using Serilog;

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
        Title = "Ingestion API",
        Version = "v1",
        Description = "Ingests source medical documents with optional OCR normalization."
    });
});
builder.Services.AddHealthChecks();

builder.Services.AddPlatformSecurity(builder.Configuration, options =>
{
    options.AddPolicy("IngestionWriters", policy =>
        policy.RequireRole("admin", "svc.ingestion", "svc.gateway"));
});

builder.Services.AddProblemDetails();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation());

builder.Services.AddHttpClient("document-storage")
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

var jobs = new ConcurrentDictionary<Guid, IngestionJobState>();
var idempotencyMap = new ConcurrentDictionary<string, Guid>();

app.MapPost("/api/ingestion/jobs", (IngestionRequest request, HttpContext context) =>
    {
        var idempotencyKey = context.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(idempotencyKey) &&
            idempotencyMap.TryGetValue(idempotencyKey!, out var existingJobId) &&
            jobs.TryGetValue(existingJobId, out var existingJob))
        {
            return Results.Created($"/api/ingestion/jobs/{existingJob.JobId}", existingJob.ToResponse());
        }

        var job = IngestionJobState.Create(request);
        jobs[job.JobId] = job;

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            idempotencyMap.TryAdd(idempotencyKey!, job.JobId);
        }

        return Results.Created($"/api/ingestion/jobs/{job.JobId}", job.ToResponse());
    })
    .AddEndpointFilter(new ValidationFilter<IngestionRequest>())
    .Produces<IngestionJobResponse>(StatusCodes.Status201Created)
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
    .WithName("CreateIngestionJob")
    .RequireAuthorization("IngestionWriters");

app.MapGet("/api/ingestion/jobs/{jobId:guid}", (Guid jobId) =>
    jobs.TryGetValue(jobId, out var job)
        ? Results.Ok(job.ToResponse())
        : Results.NotFound(new ErrorResponse("not_found", "Ingestion job not found.")))
    .Produces<IngestionJobResponse>()
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
    .WithName("GetIngestionJob")
    .RequireAuthorization();

app.MapPost("/api/ingestion/jobs/{jobId:guid}/retry", (Guid jobId, IngestionRetryRequest request) =>
    {
        if (!jobs.TryGetValue(jobId, out var job))
        {
            return Results.NotFound(new ErrorResponse("not_found", "Ingestion job not found."));
        }

        var retried = job with
        {
            Status = IngestionStatus.Pending,
            UpdatedAt = DateTimeOffset.UtcNow,
            LastError = null
        };

        jobs[jobId] = retried;
        return Results.Ok(retried.ToResponse());
    })
    .AddEndpointFilter(new ValidationFilter<IngestionRetryRequest>())
    .Produces<IngestionJobResponse>()
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
    .RequireAuthorization("IngestionWriters");

app.Run();

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() => HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() => HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

internal sealed record IngestionJobState
{
    public Guid JobId { get; init; } = Guid.NewGuid();
    public string DocumentId { get; init; } = string.Empty;
    public string SourceUri { get; init; } = string.Empty;
    public IngestionStatus Status { get; init; } = IngestionStatus.Pending;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool OcrRequired { get; init; }
    public string? Locale { get; init; }
    public IDictionary<string, string>? Metadata { get; init; }
    public string? LastError { get; init; }

    public static IngestionJobState Create(IngestionRequest request) => new()
    {
        DocumentId = request.DocumentId,
        SourceUri = request.SourceUri,
        OcrRequired = request.OcrRequired,
        Locale = request.Locale,
        Metadata = request.Metadata is null ? null : new Dictionary<string, string>(request.Metadata)
    };

    public IngestionJobResponse ToResponse() => new(
        JobId,
        DocumentId,
        SourceUri,
        Status,
        CreatedAt,
        UpdatedAt,
        OcrRequired,
        Locale,
        Metadata ?? new Dictionary<string, string>(),
        LastError
    );
}

public partial class Program;
