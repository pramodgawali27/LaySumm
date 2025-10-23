using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using LaySumm.Api.Configuration;
using LaySumm.Api.Processing.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AzureOpenAIClient = Azure.AI.OpenAI.OpenAIClient;

namespace LaySumm.Api.Processing.Summarization;

public sealed class SummarizationService
{
    private readonly AzureOpenAIClient _client;
    private readonly AzureOpenAIOptions _options;
    private readonly ILogger<SummarizationService> _logger;

    public SummarizationService(IOptions<AzureOpenAIOptions> options, ILogger<SummarizationService> logger)
    {
        _options = options.Value;
        _client = new AzureOpenAIClient(new Uri(_options.Endpoint), new AzureKeyCredential(_options.Key));
        _logger = logger;
    }

    public async Task<IReadOnlyList<SummarySection>> SummarizeAsync(
        StructuredDocument document,
        IReadOnlyList<DocumentSection> sections,
        string? userPrompt,
        string instructions,
        CancellationToken cancellationToken)
    {
        var results = new List<SummarySection>();

        foreach (var section in sections)
        {
            var prompt = BuildPrompt(document, section, userPrompt);
            var completion = await InvokeChatAsync(instructions, prompt, cancellationToken);
            results.Add(ParseSection(section, completion));
        }

        return results;
    }

    private async Task<string> InvokeChatAsync(string instructions, string prompt, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Requesting Azure OpenAI summary with deployment {Deployment}", _options.Deployment);
        var chatOptions = new ChatCompletionsOptions
        {
            DeploymentName = _options.Deployment,
            Temperature = 0.2f,
            MaxTokens = 1024
        };
        chatOptions.Messages.Add(new ChatRequestSystemMessage(instructions));
        chatOptions.Messages.Add(new ChatRequestUserMessage(prompt));

        var response = await _client.GetChatCompletionsAsync(chatOptions, cancellationToken);
        return response.Value.Choices[0].Message.Content;
    }

    private static SummarySection ParseSection(DocumentSection section, string completion)
    {
        try
        {
            var result = JsonSerializer.Deserialize<SummaryDraft>(completion, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            if (result is null)
            {
                throw new InvalidOperationException("Summary draft is null.");
            }

            return new SummarySection
            {
                SectionId = section.SectionId,
                Title = section.Title,
                PlainLanguageSummary = result.Summary ?? string.Empty,
                Citations = result.Citations ?? Array.Empty<string>(),
                WhyItMatters = result.WhyItMatters,
                Limitations = result.Limitations,
                Glossary = result.Glossary
            };
        }
        catch (JsonException)
        {
            return new SummarySection
            {
                SectionId = section.SectionId,
                Title = section.Title,
                PlainLanguageSummary = completion,
                Citations = section.Spans.Select(span => span.SourceReference).Distinct().ToList(),
                WhyItMatters = null,
                Limitations = null,
                Glossary = null
            };
        }
    }

    private static string BuildPrompt(StructuredDocument document, DocumentSection section, string? userPrompt)
    {
        var content = string.Join("\n", section.Spans.Select(span => $"[{span.SpanId}] {span.Text}"));
        var citations = string.Join(", ", section.Spans.Select(span => $"{span.SpanId}:{span.SourceReference}"));

        var instructions = new
        {
            document = document.FileName,
            section = section.Title,
            spans = content,
            citations,
            userPrompt,
            output = new
            {
                summary = "Plain-language paragraph with inline citation keys like [span-id].",
                whyItMatters = "Brief explanation of why this section matters to patients.",
                limitations = "List key limitations or uncertainties.",
                glossary = "Define jargon terms in simple words.",
                citations = "Array of citation keys used in the summary."
            }
        };

        return JsonSerializer.Serialize(instructions, new JsonSerializerOptions { WriteIndented = true });
    }

    private sealed class SummaryDraft
    {
        [JsonPropertyName("summary")]
        public string? Summary { get; init; }

        [JsonPropertyName("citations")]
        public IReadOnlyList<string>? Citations { get; init; }

        [JsonPropertyName("whyItMatters")]
        public string? WhyItMatters { get; init; }

        [JsonPropertyName("limitations")]
        public string? Limitations { get; init; }

        [JsonPropertyName("glossary")]
        public string? Glossary { get; init; }
    }
}
