using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using LaySumm.Api.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Audio;

namespace LaySumm.Api.Processing.Ingestion;

public sealed class AudioTranscriptionService
{
    private readonly AzureOpenAIOptions _azureOptions;
    private readonly ILogger<AudioTranscriptionService> _logger;
    private readonly OpenAIClient? _openAiClient;
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

        try
        {
            var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (!string.IsNullOrEmpty(openAiKey))
            {
                _openAiClient = new OpenAIClient(openAiKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize OpenAI client for ASR fallback.");
        }
    }

    public async Task<TranscriptionResult> TranscribeAsync(string documentId, IngestedFile file, CancellationToken cancellationToken)
    {
        if (_azureClient is not null)
        {
            try
            {
                var response = await _azureClient.GetAudioTranscriptionAsync(
                    _azureOptions.Deployment,
                    BinaryData.FromBytes(file.Content),
                    cancellationToken: cancellationToken);

                if (response.Value is not null)
                {
                    return MapToResult(documentId, response.Value.Text, response.Value.Segments);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Azure OpenAI transcription failed for {DocumentId}, falling back to OpenAI API.", documentId);
            }
        }

        if (_openAiClient is not null)
        {
            try
            {
                var request = new TranscriptionsEndpointCreateRequest
                {
                    File = file.Content,
                    Model = "whisper-1"
                };

                var openAiResponse = await _openAiClient.AudioEndpoint.CreateTranscriptionAsync(request, cancellationToken);
                return MapToResult(documentId, openAiResponse.Text ?? string.Empty, openAiResponse.Segments);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OpenAI transcription fallback failed for {DocumentId}.", documentId);
            }
        }

        _logger.LogWarning("No ASR provider available. Returning empty transcript for {DocumentId}.", documentId);
        return new TranscriptionResult(documentId, TimeSpan.Zero, Array.Empty<TranscriptionSegment>());
    }

    private static TranscriptionResult MapToResult(
        string documentId,
        string text,
        IReadOnlyList<AudioSpeechRecognitionSegment> segments)
    {
        var mappedSegments = segments.Select(segment => new TranscriptionSegmentResult(
            $"{documentId}-segment-{segment.Index}",
            segment.Text,
            TimeSpan.FromSeconds(segment.Start),
            TimeSpan.FromSeconds(segment.End))).ToList();

        var duration = mappedSegments.Count == 0
            ? TimeSpan.Zero
            : mappedSegments.Max(segment => segment.End);

        return new TranscriptionResult(documentId, duration, mappedSegments);
    }

    [SuppressMessage("Reliability", "CA2000", Justification = "BinaryData handles stream disposal")]
    private static TranscriptionResult MapToResult(
        string documentId,
        string text,
        IReadOnlyList<OpenAI.Audio.TranscriptionSegment>? segments)
    {
        var mappedSegments = segments?.Select(segment => new TranscriptionSegmentResult(
            $"{documentId}-segment-{segment.Id}",
            segment.Text ?? string.Empty,
            TimeSpan.FromSeconds(segment.Start ?? 0),
            TimeSpan.FromSeconds(segment.End ?? 0))).ToList() ?? new List<TranscriptionSegmentResult>();

        var duration = mappedSegments.Count == 0
            ? TimeSpan.Zero
            : mappedSegments.Max(segment => segment.End);

        return new TranscriptionResult(documentId, duration, mappedSegments);
    }
}

public sealed record TranscriptionResult(string DocumentId, TimeSpan Duration, IReadOnlyList<TranscriptionSegmentResult> Segments);

public sealed record TranscriptionSegmentResult(string SegmentId, string Text, TimeSpan Start, TimeSpan End);
