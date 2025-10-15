using System.Net.Http;
using Microsoft.OpenApi.Models;
using Platform.Api;
using Platform.Api.Validation;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using AgentOrchestratorA4.Api.Contracts;
using AgentOrchestratorA4.Api.Orchestration;
using Platform.AgentFramework.Agents;
using Platform.AgentFramework.Workflows;

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
        Title = "Agent Orchestrator A4 API",
        Version = "v1",
        Description = "Agentic workflow orchestration service."
    });
});
builder.Services.AddHealthChecks();

builder.Services.AddPlatformSecurity(builder.Configuration);

builder.Services.AddProblemDetails();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation());

builder.Services.AddHttpClient("agent-orchestrator-a4")
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

builder.Services.AddLaySummAgents(builder.Configuration);
builder.Services.AddSingleton<PlainLanguageSummaryWorkflow>();

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

app.MapGet("/api/agent-orchestrator-a4/status", () =>
    Results.Ok(new ServiceStatus("Agent Orchestrator A4 API", "v1", DateTimeOffset.UtcNow)))
    .Produces<ServiceStatus>()
    .RequireAuthorization();

app.MapGet("/api/agent-orchestrator-a4/workflow", (PlainLanguageSummaryWorkflow workflow) =>
    {
        var descriptor = workflow.Describe();
        return Results.Ok(WorkflowBlueprintResponse.FromDescriptor(descriptor));
    })
    .Produces<WorkflowBlueprintResponse>()
    .RequireAuthorization();

app.MapPost("/api/agent-orchestrator-a4/preview", (AgentOrchestratorA4Request request) =>
    Results.Ok(new AgentOrchestratorA4Response(request.ReferenceId, $"Agent Orchestrator A4 API preview generated", DateTimeOffset.UtcNow)))
    .AddEndpointFilter(new ValidationFilter<AgentOrchestratorA4Request>())
    .Produces<AgentOrchestratorA4Response>()
    .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
    .RequireAuthorization();

app.Run();

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy() => HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)));

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy() => HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(4, TimeSpan.FromSeconds(30));

public partial class Program;
