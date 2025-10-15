using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using LaySumm.Api.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AzureOpenAIClient = Azure.AI.OpenAI.OpenAIClient;

namespace LaySumm.Api.Processing.Ingestion;

public sealed class AudioTranscriptionService
{
    private readonly AzureOpenAIOptions _azureOptions;
    private readonly ILogger<AudioTranscriptionService> _logger;
    private readonly AzureOpenAIClient? _azureClient;

    public AudioTranscriptionService(
        IOptions<AzureOpenAIOptions> azureOptions,
        ILogger<AudioTranscriptionService> logger)
    {
        _azureOptions = azureOptions.Value;
        _logger = logger;

        try
        {
            _azureClient = new AzureOpenAIClient(new Uri(_azureOptions.Endpoint), new AzureKeyCredential(_azureOptions.Key));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize Azure OpenAI client for ASR. Audio transcription will use fallback.");
        }

        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrEmpty(openAiKey))
        {
            _logger.LogWarning("OPENAI_API_KEY is set, but OpenAI fallback is currently disabled for audio transcription.");
        }
    }

    public async Task<TranscriptionResult> TranscribeAsync(string documentId, IngestedFile file, CancellationToken cancellationToken)
    {
        if (_azureClient is not null)
        {
            try
            {
                var options = new AudioTranscriptionOptions
                {
                    AudioData = BinaryData.FromBytes(file.Content),
                    DeploymentName = _azureOptions.Deployment,
                    ResponseFormat = AudioTranscriptionFormat.Verbose,
                    Filename = file.FileName
                };

                var response = await _azureClient.GetAudioTranscriptionAsync(options, cancellationToken);

                if (response.Value is not null)
                {
                    return MapToResult(documentId, response.Value.Segments);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Azure OpenAI transcription failed for {DocumentId}, falling back to OpenAI API.", documentId);
            }
        }
        _logger.LogWarning("No ASR provider available. Returning empty transcript for {DocumentId}.", documentId);
        return new TranscriptionResult(documentId, TimeSpan.Zero, Array.Empty<TranscriptionSegmentResult>());
    }

    private static TranscriptionResult MapToResult(
        string documentId,
        IReadOnlyList<AudioTranscriptionSegment> segments)
    {
        var mappedSegments = segments.Select(segment => new TranscriptionSegmentResult(
            $"{documentId}-segment-{segment.Id}",
            segment.Text ?? string.Empty,
            segment.Start,
            segment.End)).ToList();

        var duration = mappedSegments.Count == 0
            ? TimeSpan.Zero
            : mappedSegments.Max(segment => segment.End);

        return new TranscriptionResult(documentId, duration, mappedSegments);
    }

}

public sealed record TranscriptionResult(string DocumentId, TimeSpan Duration, IReadOnlyList<TranscriptionSegmentResult> Segments);

public sealed record TranscriptionSegmentResult(string SegmentId, string Text, TimeSpan Start, TimeSpan End);
