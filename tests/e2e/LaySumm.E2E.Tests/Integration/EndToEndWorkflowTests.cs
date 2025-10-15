using System.Net.Http.Json;
using FluentAssertions;
using GatewayBff.Api.Contracts;
using LaySumm.E2E.Tests.Integration;
using Xunit;

namespace LaySumm.E2E.Tests;

public sealed class EndToEndWorkflowTests : IClassFixture<IngestionApiFactory>, IClassFixture<SummarizerA3ApiFactory>, IClassFixture<ValidatorApiFactory>, IClassFixture<BatchApiFactory>, IAsyncLifetime
{
    private readonly IngestionApiFactory _ingestionFactory;
    private readonly SummarizerA3ApiFactory _summarizerFactory;
    private readonly ValidatorApiFactory _validatorFactory;
    private readonly BatchApiFactory _batchFactory;
    private GatewayApiFactory? _gatewayFactory;
    private HttpClient? _gatewayClient;

    public EndToEndWorkflowTests(
        IngestionApiFactory ingestionFactory,
        SummarizerA3ApiFactory summarizerFactory,
        ValidatorApiFactory validatorFactory,
        BatchApiFactory batchFactory)
    {
        _ingestionFactory = ingestionFactory;
        _summarizerFactory = summarizerFactory;
        _validatorFactory = validatorFactory;
        _batchFactory = batchFactory;
    }

    public Task InitializeAsync()
    {
        _gatewayFactory = new GatewayApiFactory(_ingestionFactory, _summarizerFactory, _validatorFactory, _batchFactory);
        _gatewayClient = _gatewayFactory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _gatewayClient?.Dispose();
        _gatewayFactory?.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task LaneA3_EndToEnd_ReturnsSummaryAndValidation()
    {
        var request = new SummaryDispatchRequest(
            DocumentId: "DOC-001",
            SourceUri: "https://storage.local/documents/doc-001.pdf",
            Title: "Patient Discharge Summary",
            Lane: SummarizationLane.A3,
            TargetAudience: "patient",
            Metadata: new Dictionary<string, string> { ["tenant"] = "demo" },
            OcrRequired: false,
            Locale: "en",
            RequireHumanReview: false,
            Sections: new[]
            {
                new SectionInput("intro", "Introduction", new[] { "What happened?" }),
                new SectionInput("treatment", "Treatment", new[] { "What treatments were provided?" })
            },
            Retrieval: new RetrievalHints(TopK: 3, UseHybrid: true, VectorWeight: 0.6),
            MinimumReadabilityScore: 60);

        var idempotencyKey = Guid.NewGuid().ToString();

        using var createMessage = new HttpRequestMessage(HttpMethod.Post, "/api/gateway/v1/summaries")
        {
            Content = JsonContent.Create(request)
        };
        createMessage.Headers.Add("Idempotency-Key", idempotencyKey);

        using var response = await _gatewayClient!.SendAsync(createMessage);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<SummaryDispatchResponse>();
        payload.Should().NotBeNull();
        payload!.Summary.Should().NotBeNull();
        payload.Validation.Should().NotBeNull();
        payload.SentToHumanReview.Should().BeFalse();
        payload.Summary!.Sections.Should().HaveCount(2);
        payload.Validation!.MeetsReadability.Should().BeTrue();

        var status = await _gatewayClient.GetFromJsonAsync<DocumentStatusView>($"/api/gateway/v1/documents/{request.DocumentId}");
        status.Should().NotBeNull();
        status!.SummaryStatus.Should().Be("completed");
        status.Validation.Should().NotBeNull();
        status.Validation!.NumericConsistencyPassed.Should().BeTrue();

        using var repeatMessage = new HttpRequestMessage(HttpMethod.Post, "/api/gateway/v1/summaries")
        {
            Content = JsonContent.Create(request)
        };
        repeatMessage.Headers.Add("Idempotency-Key", idempotencyKey);

        using var repeat = await _gatewayClient.SendAsync(repeatMessage);
        repeat.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        var repeatPayload = await repeat.Content.ReadFromJsonAsync<SummaryDispatchResponse>();
        repeatPayload.Should().NotBeNull();
        repeatPayload!.SummaryId.Should().Be(payload.SummaryId);
    }

    [Fact]
    public async Task BatchEndpoint_SchedulesJob()
    {
        var request = new BatchGatewayRequest(
            JobName: "batch-demo",
            DocumentIds: new[] { "DOC-101", "DOC-102" },
            Lane: SummarizationLane.A3,
            ScheduledFor: DateTimeOffset.UtcNow.AddMinutes(5),
            Priority: 4,
            DeduplicateDocuments: true,
            EnablePiiRedaction: true);

        using var response = await _gatewayClient!.PostAsJsonAsync("/api/gateway/v1/batch", request);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<BatchGatewayResponse>();
        payload.Should().NotBeNull();
        payload!.JobName.Should().Be("batch-demo");
        payload.DocumentIds.Should().HaveCount(2);
        payload.Status.Should().Be(GatewayBatchStatus.Scheduled);
    }
}
