using System.Collections.Generic;
using System.Linq;
using LaySumm.Api.Models.Requests;
using Microsoft.Extensions.Logging;

namespace LaySumm.Api.Services;

public sealed class SupervisorWorkflow
{
    private readonly WorkflowJob _job;
    private readonly WorkflowAgents _agents;
    private readonly ILogger<SupervisorWorkflow> _logger;

    public SupervisorWorkflow(WorkflowJob job, WorkflowAgents agents, ILogger<SupervisorWorkflow> logger)
    {
        _job = job;
        _agents = agents;
        _logger = logger;
    }

    public async Task<WorkflowResult> ExecuteAsync(CancellationToken cancellationToken)
    {
        var context = new WorkflowExecutionContext(_job.Request);

        await RunReaderAsync(context, cancellationToken);
        await RunIndexerAsync(context, cancellationToken);

        if (!string.IsNullOrWhiteSpace(_job.Request.UserPrompt))
        {
            await RunRetrieverAsync(context, cancellationToken);
        }
        else
        {
            context.SelectedSections = context.DocumentManifest.Select(section => section.SectionId).ToList();
        }

        await RunSummarizerAsync(context, cancellationToken);
        await RunVisualizerAsync(context, cancellationToken);
        await RunAssemblerAsync(context, cancellationToken);

        return new WorkflowResult
        {
            JsonUri = context.OutputJsonUri,
            HtmlUri = context.OutputHtmlUri,
            DocxUri = context.OutputDocxUri,
            PdfUri = context.OutputPdfUri
        };
    }

    private Task RunReaderAsync(WorkflowExecutionContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[{JobId}] {Agent} processing {DocumentCount} documents. Instructions: {Instructions}", _job.Id, _agents.Reader.Name, _job.Request.Documents.Count, _agents.Reader.Instructions);
        context.DocumentManifest = _job.Request.Documents.Select((doc, index) => new DocumentSection($"doc-{index}", doc.FileName, new List<DocumentSpan>())).ToList();
        return Task.CompletedTask;
    }

    private Task RunIndexerAsync(WorkflowExecutionContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[{JobId}] {Agent} building search index. Instructions: {Instructions}", _job.Id, _agents.Indexer.Name, _agents.Indexer.Instructions);
        context.IndexBuilt = true;
        return Task.CompletedTask;
    }

    private Task RunRetrieverAsync(WorkflowExecutionContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[{JobId}] {Agent} selecting spans for prompt. Instructions: {Instructions}", _job.Id, _agents.Retriever.Name, _agents.Retriever.Instructions);
        context.SelectedSections = context.DocumentManifest
            .Select(section => section.SectionId)
            .Take(8)
            .ToList();
        return Task.CompletedTask;
    }

    private Task RunSummarizerAsync(WorkflowExecutionContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[{JobId}] {Agent} producing PLS sections. Instructions: {Instructions}", _job.Id, _agents.Summarizer.Name, _agents.Summarizer.Instructions);
        context.SummaryHtml = "<p>Plain-language summary placeholder.</p>";
        context.SummaryJson = "{\"summary\":\"placeholder\"}";
        return Task.CompletedTask;
    }

    private Task RunVisualizerAsync(WorkflowExecutionContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[{JobId}] {Agent} generating explanatory visuals. Instructions: {Instructions}", _job.Id, _agents.Visualizer.Name, _agents.Visualizer.Instructions);
        context.GeneratedVisuals.Add(new GeneratedVisual
        {
            Prompt = "Diagram explaining key medical concept",
            AssetUri = null,
            Caption = "Illustrative placeholder diagram",
            AltText = "Diagram describing the main idea of the summary"
        });
        return Task.CompletedTask;
    }

    private Task RunAssemblerAsync(WorkflowExecutionContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[{JobId}] {Agent} compiling outputs. Instructions: {Instructions}", _job.Id, _agents.Assembler.Name, _agents.Assembler.Instructions);
        context.OutputJsonUri = new Uri($"https://storage.example.com/pls/{_job.Id}/result.json");
        context.OutputHtmlUri = new Uri($"https://storage.example.com/pls/{_job.Id}/result.html");
        context.OutputDocxUri = new Uri($"https://storage.example.com/pls/{_job.Id}/result.docx");
        context.OutputPdfUri = new Uri($"https://storage.example.com/pls/{_job.Id}/result.pdf");
        return Task.CompletedTask;
    }
}

internal sealed class WorkflowExecutionContext
{
    public WorkflowExecutionContext(PlainLanguageSummaryRequest request)
    {
        Request = request;
    }

    public PlainLanguageSummaryRequest Request { get; }
    public List<DocumentSection> DocumentManifest { get; set; } = new();
    public bool IndexBuilt { get; set; }
    public List<string> SelectedSections { get; set; } = new();
    public string? SummaryHtml { get; set; }
    public string? SummaryJson { get; set; }
    public List<GeneratedVisual> GeneratedVisuals { get; } = new();
    public Uri? OutputJsonUri { get; set; }
    public Uri? OutputHtmlUri { get; set; }
    public Uri? OutputDocxUri { get; set; }
    public Uri? OutputPdfUri { get; set; }
}

public sealed record DocumentSection(string SectionId, string Title, IReadOnlyList<DocumentSpan> Spans);

public sealed record DocumentSpan(string SpanId, string SourceReference, string Text);

public sealed class GeneratedVisual
{
    public required string Prompt { get; init; }
    public Uri? AssetUri { get; init; }
    public required string Caption { get; init; }
    public required string AltText { get; init; }
}
