using System.Net.Http;
using GatewayBff.Api.Contracts;
using GatewayBff.Api.External;
using GatewayBff.Api.Workflows;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Options;
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

builder.Services.Configure<GatewayServiceOptions>(builder.Configuration.GetSection("Services"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Gateway BFF API",
        Version = "v1",
        Description = "Aggregated BFF for admin and end-user frontends."
    });
});
builder.Services.AddHealthChecks();

builder.Services.AddPlatformSecurity(builder.Configuration, options =>
{
    options.AddPolicy("GatewayWriters", policy => policy.RequireRole("admin", "svc.gateway"));
});

builder.Services.AddProblemDetails();
builder.Services.AddSingleton<IDocumentStateStore, InMemoryDocumentStateStore>();
builder.Services.AddScoped<DocumentWorkflowOrchestrator>();
builder.Services.AddScoped<DocumentStatusService>();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation());

builder.Services.AddHttpClient<IIngestionClient, IngestionClient>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<GatewayServiceOptions>>().Value;
        client.BaseAddress = new Uri(options.IngestionBaseUrl);
    })
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

builder.Services.AddHttpClient<ISummarizerA3Client, SummarizerA3Client>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<GatewayServiceOptions>>().Value;
        client.BaseAddress = new Uri(options.SummarizerA3BaseUrl);
    })
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

builder.Services.AddHttpClient<IValidatorClient, ValidatorClient>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<GatewayServiceOptions>>().Value;
        client.BaseAddress = new Uri(options.ValidatorBaseUrl);
    })
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

builder.Services.AddHttpClient<IBatchClient, BatchClient>((sp, client) =>
    {
        var options = sp.GetRequiredService<IOptions<GatewayServiceOptions>>().Value;
        client.BaseAddress = new Uri(options.BatchBaseUrl);
    })
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

app.MapGet("/api/gateway/v1/status", () =>
        Results.Ok(new ServiceStatus("Gateway BFF API", "v1", DateTimeOffset.UtcNow)))
    .Produces<ServiceStatus>()
    .RequireAuthorization();

app.MapPost("/api/gateway/v1/summaries", async (
        SummaryDispatchRequest request,
        DocumentWorkflowOrchestrator orchestrator,
        IDocumentStateStore stateStore,
        HttpContext context,
        CancellationToken cancellationToken) =>
    {
        var idempotencyKey = context.Request.Headers["Idempotency-Key"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(idempotencyKey) &&
            stateStore.TryGetByIdempotencyKey(idempotencyKey!, out var existingResponse))
        {
            return Results.Ok(existingResponse);
        }

        try
        {
            var response = await orchestrator.DispatchAsync(request, idempotencyKey, cancellationToken);
            return Results.Created($"/api/gateway/v1/documents/{response.DocumentId}", response);
        }
        catch (NotSupportedException ex)
        {
            return Results.Json(new ErrorResponse("lane_not_supported", ex.Message),
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Json(new ErrorResponse("invalid_request", ex.Message),
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (GatewayHttpException ex)
        {
            return Results.Json(new ErrorResponse("dependency_error", ex.Message, TraceId: context.TraceIdentifier),
                statusCode: (int)ex.StatusCode);
        }
    })
    .AddEndpointFilter(new ValidationFilter<SummaryDispatchRequest>())
    .Produces<SummaryDispatchResponse>(StatusCodes.Status201Created)
    .Produces<SummaryDispatchResponse>(StatusCodes.Status200OK)
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
    .Produces<ErrorResponse>(StatusCodes.Status424FailedDependency)
    .RequireAuthorization("GatewayWriters")
    .WithName("DispatchSummary");

app.MapGet("/api/gateway/v1/documents/{documentId}", async (
        string documentId,
        DocumentStatusService statusService,
        CancellationToken cancellationToken) =>
    {
        try
        {
            var status = await statusService.GetAsync(documentId, cancellationToken);
            return Results.Ok(status);
        }
        catch (KeyNotFoundException)
        {
            return Results.Json(new ErrorResponse("not_found", "Document not found."),
                statusCode: StatusCodes.Status404NotFound);
        }
        catch (GatewayHttpException ex)
        {
            return Results.Json(new ErrorResponse("dependency_error", ex.Message),
                statusCode: (int)ex.StatusCode);
        }
    })
    .Produces<DocumentStatusView>()
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
    .Produces<ErrorResponse>(StatusCodes.Status424FailedDependency)
    .RequireAuthorization()
    .WithName("GetDocumentStatus");

app.MapPost("/api/gateway/v1/batch", async (
        BatchGatewayRequest request,
        IBatchClient batchClient,
        HttpContext context,
        CancellationToken cancellationToken) =>
    {
        var idempotencyKey = context.Request.Headers["Idempotency-Key"].FirstOrDefault();

        try
        {
            var response = await batchClient.CreateAsync(new BatchJobCreateRequest(
                request.JobName,
                request.DocumentIds,
                request.Lane.ToString(),
                request.ScheduledFor,
                request.Priority,
                request.DeduplicateDocuments,
                request.EnablePiiRedaction),
                idempotencyKey,
                cancellationToken);

            var status = Enum.Parse<GatewayBatchStatus>(response.Status.ToString(), ignoreCase: true);

            var gatewayResponse = new BatchGatewayResponse(
                response.JobId,
                response.JobName,
                response.Lane,
                status,
                response.CreatedAt,
                response.ScheduledFor,
                response.Priority,
                response.EnablePiiRedaction,
                response.DocumentIds);

            return Results.Created($"/api/gateway/v1/batch/{gatewayResponse.JobId}", gatewayResponse);
        }
        catch (GatewayHttpException ex)
        {
            return Results.Json(new ErrorResponse("dependency_error", ex.Message),
                statusCode: (int)ex.StatusCode);
        }
    })
    .AddEndpointFilter(new ValidationFilter<BatchGatewayRequest>())
    .Produces<BatchGatewayResponse>(StatusCodes.Status201Created)
    .Produces<ErrorResponse>(StatusCodes.Status424FailedDependency)
    .RequireAuthorization("GatewayWriters")
    .WithName("CreateBatchJob");

app.Run();

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() => HttpPolicyExtensions
    .HandleTransientHttpError()
    .OrResult(response => (int)response.StatusCode == StatusCodes.Status429TooManyRequests)
    .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() => HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(4, TimeSpan.FromSeconds(30));

public partial class Program;
