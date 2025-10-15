using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LaySumm.Api.Models.Requests;
using LaySumm.Api.Processing.Models;
using Microsoft.Extensions.Logging;

namespace LaySumm.Api.Services;

public sealed class SupervisorWorkflow
{
    private readonly WorkflowJob _job;
    private readonly WorkflowAgents _agents;
    private readonly Processing.Ingestion.FileIngestionService _fileIngestionService;
    private readonly Processing.Ingestion.DocumentParser _documentParser;
    private readonly Processing.Indexing.DocumentIndexer _documentIndexer;
    private readonly Processing.Retrieval.DocumentRetriever _documentRetriever;
    private readonly Processing.Summarization.SummarizationService _summarizationService;
    private readonly Processing.Visualization.VisualizationService _visualizationService;
    private readonly Processing.Assembly.DocumentAssembler _documentAssembler;
    private readonly ILogger<SupervisorWorkflow> _logger;

    public SupervisorWorkflow(
        WorkflowJob job,
        WorkflowAgents agents,
        Processing.Ingestion.FileIngestionService fileIngestionService,
        Processing.Ingestion.DocumentParser documentParser,
        Processing.Indexing.DocumentIndexer documentIndexer,
        Processing.Retrieval.DocumentRetriever documentRetriever,
        Processing.Summarization.SummarizationService summarizationService,
        Processing.Visualization.VisualizationService visualizationService,
        Processing.Assembly.DocumentAssembler documentAssembler,
        ILogger<SupervisorWorkflow> logger)
    {
        _job = job;
        _agents = agents;
        _fileIngestionService = fileIngestionService;
        _documentParser = documentParser;
        _documentIndexer = documentIndexer;
        _documentRetriever = documentRetriever;
        _summarizationService = summarizationService;
        _visualizationService = visualizationService;
        _documentAssembler = documentAssembler;
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
            context.SectionSelections = context.Documents
                .SelectMany(document => document.Sections.Select(section => new DocumentSelection(document, section)))
                .ToList();
        }

        await RunSummarizerAsync(context, cancellationToken);
        await RunVisualizerAsync(context, cancellationToken);
        await RunAssemblerAsync(context, cancellationToken);

        return new WorkflowResult
        {
            JsonUri = context.AssemblyResult?.JsonUri,
            HtmlUri = context.AssemblyResult?.HtmlUri,
            DocxUri = context.AssemblyResult?.DocxUri,
            PdfUri = context.AssemblyResult?.PdfUri
        };
    }

    private async Task RunReaderAsync(WorkflowExecutionContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[{JobId}] {Agent} processing {DocumentCount} documents. Instructions: {Instructions}", _job.Id, _agents.Reader.Name, _job.Request.Documents.Count, _agents.Reader.Instructions);

        var documents = new List<Processing.Models.StructuredDocument>();
        var index = 0;
        foreach (var document in _job.Request.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ingested = await _fileIngestionService.DownloadAsync(document, cancellationToken);
            var parsed = await _documentParser.ParseAsync($"doc-{index}", ingested, cancellationToken);
            documents.Add(parsed);
            index++;
        }

        context.Documents = documents;
    }

    private async Task RunIndexerAsync(WorkflowExecutionContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[{JobId}] {Agent} building search index. Instructions: {Instructions}", _job.Id, _agents.Indexer.Name, _agents.Indexer.Instructions);
        await _documentIndexer.EnsureIndexAsync(cancellationToken);
        foreach (var document in context.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _documentIndexer.IndexAsync(document, cancellationToken);
        }
        context.IndexBuilt = true;
    }

    private async Task RunRetrieverAsync(WorkflowExecutionContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[{JobId}] {Agent} selecting spans for prompt. Instructions: {Instructions}", _job.Id, _agents.Retriever.Name, _agents.Retriever.Instructions);
        var prompt = _job.Request.UserPrompt!;
        var retrievedSpans = await _documentRetriever.RetrieveAsync(prompt, 8, cancellationToken);
        var manifest = context.Documents
            .SelectMany(doc => doc.Sections.Select(section => new DocumentSelection(doc, section)))
            .ToDictionary(selection => (selection.Document.DocumentId, selection.Section.SectionId));

        context.SectionSelections = new List<DocumentSelection>();
        foreach (var span in retrievedSpans)
        {
            if (manifest.TryGetValue((span.DocumentId, span.SectionId), out var selection))
            {
                if (!context.SectionSelections.Any(existing => existing.Section.SectionId == selection.Section.SectionId))
                {
                    context.SectionSelections.Add(selection);
                }
            }
        }

        if (context.SectionSelections.Count == 0)
        {
            context.SectionSelections = manifest.Values.Take(3).ToList();
        }
    }

    private async Task RunSummarizerAsync(WorkflowExecutionContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[{JobId}] {Agent} producing PLS sections. Instructions: {Instructions}", _job.Id, _agents.Summarizer.Name, _agents.Summarizer.Instructions);
        context.SummarySections = new List<Processing.Summarization.SummarySection>();

        var groupedSelections = context.SectionSelections
            .GroupBy(selection => selection.Document)
            .ToList();

        foreach (var group in groupedSelections)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var summaries = await _summarizationService.SummarizeAsync(
                group.Key,
                group.Select(selection => selection.Section).ToList(),
                _job.Request.UserPrompt,
                _agents.Summarizer.Instructions,
                cancellationToken);
            context.SummarySections.AddRange(summaries);
        }
    }

    private async Task RunVisualizerAsync(WorkflowExecutionContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[{JobId}] {Agent} generating explanatory visuals. Instructions: {Instructions}", _job.Id, _agents.Visualizer.Name, _agents.Visualizer.Instructions);
        var compositeDocument = CreateCompositeDocument(context);
        var visuals = await _visualizationService.GenerateAsync(compositeDocument, context.SummarySections, _agents.Visualizer.Instructions, cancellationToken);
        context.GeneratedVisuals = visuals.ToList();
    }

    private async Task RunAssemblerAsync(WorkflowExecutionContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[{JobId}] {Agent} compiling outputs. Instructions: {Instructions}", _job.Id, _agents.Assembler.Name, _agents.Assembler.Instructions);
        var compositeDocument = CreateCompositeDocument(context);
        var result = await _documentAssembler.AssembleAsync(compositeDocument, context.SummarySections, context.GeneratedVisuals, cancellationToken);
        context.AssemblyResult = result;
    }

    private Processing.Models.StructuredDocument CreateCompositeDocument(WorkflowExecutionContext context)
    {
        var sections = context.SectionSelections.Select(selection => selection.Section).DistinctBy(section => section.SectionId).ToList();
        return new Processing.Models.StructuredDocument
        {
            DocumentId = _job.Id,
            FileName = string.Join(", ", context.Documents.Select(document => document.FileName)),
            Sections = sections,
            Figures = context.Documents.SelectMany(document => document.Figures).ToList(),
            Tables = context.Documents.SelectMany(document => document.Tables).ToList(),
            Metadata = new Dictionary<string, string>
            {
                ["SourceDocuments"] = string.Join(",", context.Documents.Select(document => document.FileName)),
                ["HasUserPrompt"] = (!string.IsNullOrWhiteSpace(_job.Request.UserPrompt)).ToString()
            }
        };
    }
}

internal sealed class WorkflowExecutionContext
{
    public WorkflowExecutionContext(PlainLanguageSummaryRequest request)
    {
        Request = request;
    }

    public PlainLanguageSummaryRequest Request { get; }
    public List<Processing.Models.StructuredDocument> Documents { get; set; } = new();
    public bool IndexBuilt { get; set; }
    public List<DocumentSelection> SectionSelections { get; set; } = new();
    public List<Processing.Summarization.SummarySection> SummarySections { get; set; } = new();
    public List<GeneratedVisual> GeneratedVisuals { get; set; } = new();
    public Processing.Assembly.AssemblyResult? AssemblyResult { get; set; }
}

public sealed record DocumentSelection(Processing.Models.StructuredDocument Document, Processing.Models.DocumentSection Section);

public sealed class GeneratedVisual
{
    public required string Prompt { get; init; }
    public Uri? AssetUri { get; init; }
    public required string Caption { get; init; }
    public required string AltText { get; init; }
}
