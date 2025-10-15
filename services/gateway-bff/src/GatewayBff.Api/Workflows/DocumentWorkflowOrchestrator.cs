using GatewayBff.Api.Contracts;
using GatewayBff.Api.External;
using Microsoft.Extensions.Logging;

namespace GatewayBff.Api.Workflows;

internal sealed class DocumentWorkflowOrchestrator
{
    private readonly IIngestionClient _ingestionClient;
    private readonly ISummarizerA3Client _summarizerClient;
    private readonly IValidatorClient _validatorClient;
    private readonly IDocumentStateStore _stateStore;
    private readonly ILogger<DocumentWorkflowOrchestrator> _logger;

    public DocumentWorkflowOrchestrator(
        IIngestionClient ingestionClient,
        ISummarizerA3Client summarizerClient,
        IValidatorClient validatorClient,
        IDocumentStateStore stateStore,
        ILogger<DocumentWorkflowOrchestrator> logger)
    {
        _ingestionClient = ingestionClient;
        _summarizerClient = summarizerClient;
        _validatorClient = validatorClient;
        _stateStore = stateStore;
        _logger = logger;
    }

    public async Task<SummaryDispatchResponse> DispatchAsync(SummaryDispatchRequest request, string? idempotencyKey, CancellationToken cancellationToken)
    {
        if (request.Lane != SummarizationLane.A3)
        {
            throw new NotSupportedException($"Lane '{request.Lane}' is not yet implemented for orchestration.");
        }

        if (request.Sections is null || request.Sections.Count == 0)
        {
            throw new InvalidOperationException("At least one section must be supplied for lane A3 summarization.");
        }

        var metadata = request.Metadata is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(request.Metadata, StringComparer.OrdinalIgnoreCase);

        var ingestionJob = await _ingestionClient.CreateJobAsync(new IngestionJobCreateRequest(
            request.DocumentId,
            request.SourceUri,
            request.Title,
            request.Locale,
            request.OcrRequired,
            metadata,
            Priority: 0),
            idempotencyKey,
            cancellationToken);

        var retrievalHints = request.Retrieval ?? new RetrievalHints();
        var summarizationRequest = new RagSummarizationRequest(
            request.DocumentId,
            request.Sections.Select(section => new RagSectionPlan(section.SectionId, section.Heading, section.FocusQuestions)).ToArray(),
            new RagRetrievalOptions(
                retrievalHints.TopK,
                retrievalHints.UseHybrid,
                retrievalHints.VectorWeight,
                retrievalHints.Keywords,
                retrievalHints.SectionAnchors),
            request.TargetAudience,
            IncludeCitations: true,
            PreferredLanguage: request.Locale ?? "en",
            request.MinimumReadabilityScore);

        var summary = await _summarizerClient.CreateSummaryAsync(summarizationRequest, cancellationToken);

        var aggregatedSummaryText = string.Join("\n\n", summary.Sections.Select(section => section.Summary));
        var citationPayload = summary.Sections
            .SelectMany(section => section.Citations)
            .GroupBy(citation => citation.SourceId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(citation => new CitationEvidencePayload(citation.SourceId, citation.Snippet, citation.Uri))
            .ToArray();

        var validation = await _validatorClient.ValidateAsync(new ValidationRequestPayload(
            summary.SummaryId,
            summary.DocumentId,
            aggregatedSummaryText,
            citationPayload,
            request.MinimumReadabilityScore,
            EnforceNumericConsistency: true),
            cancellationToken);

        var requiresReview = request.RequireHumanReview ||
            !validation.MeetsReadability ||
            validation.ContainsBannedJargon ||
            !validation.NumericConsistencyPassed;

        var response = new SummaryDispatchResponse(
            request.DocumentId,
            ingestionJob.JobId,
            request.Lane,
            DateTimeOffset.UtcNow,
            new SummaryView(
                summary.SummaryId,
                summary.ModelVariant,
                summary.GeneratedAt,
                summary.EstimatedCostUsd,
                summary.Sections.Select(section => new SummarySectionView(
                    section.SectionId,
                    section.Heading,
                    section.Summary,
                    section.ReadabilityScore,
                    section.Citations.Select(citation => new CitationView(citation.SourceId, citation.Snippet, citation.Uri, citation.Score)).ToArray()
                )).ToArray()),
            new ValidationView(
                validation.FleschReadingEase,
                validation.MeetsReadability,
                validation.NumericConsistencyPassed,
                validation.ContainsBannedJargon,
                validation.Issues,
                validation.NumericFindings),
            requiresReview,
            metadata);

        _stateStore.Save(response, idempotencyKey);

        _logger.LogInformation("Dispatched document {DocumentId} through lane {Lane}", request.DocumentId, request.Lane);
        return response;
    }
}
