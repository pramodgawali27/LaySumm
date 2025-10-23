using Azure;
using Azure.Search.Documents;
using LaySumm.Api.Agents;
using LaySumm.Api.Configuration;
using LaySumm.Api.Models.Requests;
using LaySumm.Api.Models.Responses;
using LaySumm.Api.Services;
using LaySumm.Api.Processing.Assembly;
using LaySumm.Api.Processing.Ingestion;
using LaySumm.Api.Processing.Indexing;
using LaySumm.Api.Processing.Retrieval;
using LaySumm.Api.Processing.Summarization;
using LaySumm.Api.Processing.Visualization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Options;
using SearchOptionsConfig = LaySumm.Api.Configuration.SearchOptions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<AzureOpenAIOptions>()
    .Bind(builder.Configuration.GetSection(AzureOpenAIOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services.AddOptions<StorageOptions>()
    .Bind(builder.Configuration.GetSection(StorageOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services.AddOptions<SearchOptionsConfig>()
    .Bind(builder.Configuration.GetSection(SearchOptionsConfig.SectionName))
    .ValidateDataAnnotations();

builder.Services.AddOptions<ImageGenerationOptions>()
    .Bind(builder.Configuration.GetSection(ImageGenerationOptions.SectionName))
    .Validate(options =>
    {
        return options.Provider switch
        {
            ImageProvider.None => true,
            ImageProvider.OpenAI => options.OpenAI is not null && !string.IsNullOrWhiteSpace(options.OpenAI.ApiKey),
            ImageProvider.AzureOpenAI => options.AzureOpenAI is not null
                && !string.IsNullOrWhiteSpace(options.AzureOpenAI.Endpoint)
                && !string.IsNullOrWhiteSpace(options.AzureOpenAI.Key)
                && !string.IsNullOrWhiteSpace(options.AzureOpenAI.Deployment),
            _ => false
        };
    }, "Image generation configuration is invalid.")
    .ValidateOnStart();

builder.Services.AddAzureClients(factory =>
{
    factory.AddBlobServiceClient(builder.Configuration.GetSection(StorageOptions.SectionName));
});

builder.Services.AddSingleton(provider =>
{
    var options = provider.GetRequiredService<IOptions<SearchOptionsConfig>>().Value;
    return new SearchClient(new Uri(options.Endpoint), options.IndexName, new AzureKeyCredential(options.Key));
});

builder.Services.AddSingleton<AgentFactory>();
builder.Services.AddSingleton<AudioTranscriptionService>();
builder.Services.AddSingleton<FileIngestionService>();
builder.Services.AddSingleton<DocumentParser>();
builder.Services.AddSingleton<DocumentIndexer>();
builder.Services.AddSingleton<DocumentRetriever>();
builder.Services.AddSingleton<SummarizationService>();
builder.Services.AddSingleton<VisualizationService>();
builder.Services.AddSingleton<DocumentAssembler>();
builder.Services.AddSingleton<DocumentUploadService>();
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

app.MapPost("/api/pls/upload", async (HttpRequest httpRequest, DocumentUploadService uploadService, PlainLanguageSummaryOrchestrator orchestrator, CancellationToken cancellationToken) =>
{
    if (!httpRequest.HasFormContentType)
    {
        return Results.BadRequest("Request must be multipart/form-data.");
    }

    var form = await httpRequest.ReadFormAsync(cancellationToken);
    var file = form.Files["file"];
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest("Document upload is required.");
    }

    var document = await uploadService.UploadAsync(file, cancellationToken);

    var prompt = form["prompt"].ToString();
    var forceRegenerate = false;
    if (bool.TryParse(form["forceRegenerate"], out var parsed))
    {
        forceRegenerate = parsed;
    }

    var request = new PlainLanguageSummaryRequest
    {
        Documents = new[] { document },
        UserPrompt = string.IsNullOrWhiteSpace(prompt) ? null : prompt,
        ForceRegenerate = forceRegenerate
    };

    var job = await orchestrator.EnqueueAsync(request, cancellationToken);
    return Results.Accepted($"/api/pls/{job.Id}", new SubmitResponse(job.Id));
});

app.MapGet("/api/pls/{jobId}", async (string jobId, PlainLanguageSummaryOrchestrator orchestrator, CancellationToken cancellationToken) =>
{
    var status = await orchestrator.GetStatusAsync(jobId, cancellationToken);
    return status is null ? Results.NotFound() : Results.Ok(status);
});

app.Run();
