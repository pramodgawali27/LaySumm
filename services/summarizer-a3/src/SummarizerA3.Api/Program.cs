using System.Collections.Concurrent;
using System.Linq;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Options;
using Platform.Api;
using Platform.Api.Validation;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using SummarizerA3.Api.Contracts;

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
        Title = "Summarizer A3 API",
        Version = "v1",
        Description = "Retrieval-augmented summarization with citation stitching."
    });
});
builder.Services.AddHealthChecks();

builder.Services.AddPlatformSecurity(builder.Configuration, options =>
{
    options.AddPolicy("SummarizerA3Writers", policy =>
        policy.RequireRole("admin", "svc.gateway", "svc.orchestrator", "svc.batch"));
});

builder.Services.Configure<RagOptions>(builder.Configuration.GetSection("Rag"));
builder.Services.AddSingleton<RagPipeline>();
builder.Services.AddProblemDetails();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation());

builder.Services.AddHttpClient("retriever")
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

var statusStore = new ConcurrentDictionary<string, SummarizationStatusResponse>();

app.MapPost("/api/summarizer-a3/summaries", async (SummarizationRequest request, RagPipeline pipeline, HttpContext context, CancellationToken cancellationToken) =>
    {
        var summary = await pipeline.GenerateAsync(request, cancellationToken);
        statusStore[summary.SummaryId] = new SummarizationStatusResponse(summary.SummaryId, summary.DocumentId, "completed", summary.GeneratedAt);
        return Results.Created($"/api/summarizer-a3/summaries/{summary.SummaryId}", summary);
    })
    .AddEndpointFilter(new ValidationFilter<SummarizationRequest>())
    .Produces<SummarizationResponse>(StatusCodes.Status201Created)
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
    .RequireAuthorization("SummarizerA3Writers")
    .WithName("CreateRagSummary");

app.MapGet("/api/summarizer-a3/summaries/{summaryId}", (string summaryId) =>
    statusStore.TryGetValue(summaryId, out var status)
        ? Results.Ok(status)
        : Results.NotFound(new ErrorResponse("not_found", "Summary not found.")))
    .Produces<SummarizationStatusResponse>()
    .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
    .RequireAuthorization();

app.Run();

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() => HttpPolicyExtensions
    .HandleTransientHttpError()
    .OrResult(response => (int)response.StatusCode == StatusCodes.Status429TooManyRequests)
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() => HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(4, TimeSpan.FromSeconds(30));

public sealed class RagOptions
{
    public string RetrieverBaseUrl { get; set; } = "";
    public string SummarizationModel { get; set; } = "gpt-4o";
    public double MaxSectionTokens { get; set; } = 500;
}

public sealed class RagPipeline
{
    private readonly ILogger<RagPipeline> _logger;
    private readonly RagOptions _options;

    public RagPipeline(ILogger<RagPipeline> logger, IOptions<RagOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public Task<SummarizationResponse> GenerateAsync(SummarizationRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Generating RAG summary for {DocumentId} with {SectionCount} sections", request.DocumentId, request.Sections.Count);

        var summaryId = $"rag-{Guid.NewGuid():N}";
        var sections = request.Sections.Select(section =>
        {
            var citations = BuildCitations(section, request.Retrieval.TopK);
            var summaryText = $"Plain language summary for {section.Heading} tailored to {request.TargetAudience}.";
            var readability = 65 + Random.Shared.NextDouble() * 10;

            return new SectionSummary(
                section.SectionId,
                section.Heading,
                summaryText,
                readability,
                citations
            );
        }).ToList();

        var response = new SummarizationResponse(
            summaryId,
            request.DocumentId,
            sections,
            _options.SummarizationModel,
            DateTimeOffset.UtcNow,
            EstimatedCostUsd: Math.Round(sections.Count * 0.02, 4)
        );

        return Task.FromResult(response);
    }

    private static IReadOnlyList<Citation> BuildCitations(SectionPlan section, int topK)
    {
        return Enumerable.Range(1, Math.Min(topK, 3)).Select(rank =>
            new Citation(
                SourceId: $"{section.SectionId}-chunk-{rank}",
                Snippet: $"Supporting evidence snippet {rank} for {section.Heading}",
                Uri: $"https://retriever.local/documents/{section.SectionId}/chunks/{rank}",
                Score: 0.9 - (rank * 0.05))
        ).ToList();
    }
}

public partial class Program;
