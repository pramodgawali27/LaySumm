using Azure;
using Azure.Search.Documents;
using LaySumm.Api.Agents;
using LaySumm.Api.Configuration;
using LaySumm.Api.Models.Requests;
using LaySumm.Api.Models.Responses;
using LaySumm.Api.Services;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<AzureOpenAIOptions>()
    .Bind(builder.Configuration.GetSection(AzureOpenAIOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services.AddOptions<StorageOptions>()
    .Bind(builder.Configuration.GetSection(StorageOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services.AddOptions<SearchOptions>()
    .Bind(builder.Configuration.GetSection(SearchOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services.AddAzureClients(factory =>
{
    factory.AddBlobServiceClient(builder.Configuration.GetSection(StorageOptions.SectionName));
});

builder.Services.AddSingleton(provider =>
{
    var options = provider.GetRequiredService<IOptions<SearchOptions>>().Value;
    return new SearchClient(new Uri(options.Endpoint), options.IndexName, new AzureKeyCredential(options.Key));
});

builder.Services.AddSingleton<AgentFactory>();
builder.Services.AddSingleton<WorkflowBuilder>();
builder.Services.AddSingleton<PlainLanguageSummaryOrchestrator>();
builder.Services.AddSingleton<BackgroundWorkflowQueue>();
builder.Services.AddHostedService<WorkflowDispatcher>();

var app = builder.Build();

app.MapPost("/api/pls", async (PlainLanguageSummaryRequest request, PlainLanguageSummaryOrchestrator orchestrator, CancellationToken cancellationToken) =>
{
    var job = await orchestrator.EnqueueAsync(request, cancellationToken);
    return Results.Accepted($"/api/pls/{job.Id}", new SubmitResponse(job.Id));
});

app.MapGet("/api/pls/{jobId}", async (string jobId, PlainLanguageSummaryOrchestrator orchestrator, CancellationToken cancellationToken) =>
{
    var status = await orchestrator.GetStatusAsync(jobId, cancellationToken);
    return status is null ? Results.NotFound() : Results.Ok(status);
});

app.Run();
