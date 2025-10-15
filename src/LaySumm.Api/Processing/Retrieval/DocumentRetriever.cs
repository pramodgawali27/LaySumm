using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using LaySumm.Api.Configuration;
using LaySumm.Api.Processing.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LaySumm.Api.Processing.Retrieval;

public sealed class DocumentRetriever
{
    private readonly SearchClient _searchClient;
    private readonly AzureOpenAIClient _azureClient;
    private readonly AzureOpenAIOptions _options;
    private readonly ILogger<DocumentRetriever> _logger;

    public DocumentRetriever(SearchClient searchClient, IOptions<AzureOpenAIOptions> options, ILogger<DocumentRetriever> logger)
    {
        _searchClient = searchClient;
        _options = options.Value;
        _azureClient = new AzureOpenAIClient(new Uri(_options.Endpoint), new AzureKeyCredential(_options.Key));
        _logger = logger;
    }

    public async Task<IReadOnlyList<RetrievedSpan>> RetrieveAsync(string query, int top, CancellationToken cancellationToken)
    {
        var embedding = await _azureClient.GetEmbeddingsAsync(new EmbeddingsOptions(_options.Deployment, new[] { query }), cancellationToken);
        var queryVector = embedding.Value.Data.First().Embedding;

        var searchOptions = new SearchOptions
        {
            Size = top,
            QueryType = SearchQueryType.Semantic,
            SemanticConfigurationName = "default"
        };

        searchOptions.Select.Add("id");
        searchOptions.Select.Add("documentId");
        searchOptions.Select.Add("sectionId");
        searchOptions.Select.Add("spanId");
        searchOptions.Select.Add("text");
        searchOptions.Select.Add("pageNumber");
        searchOptions.Select.Add("embedding");

        var results = new List<RetrievedSpan>();
        var response = await _searchClient.SearchAsync<SearchDocument>(query, searchOptions, cancellationToken);
        await foreach (var result in response.Value.GetResultsAsync())
        {
            var document = result.Document;
            var cosine = result.Score ?? 0;
            if (document.TryGetValue("embedding", out object? embeddingValue) && TryConvertEmbedding(embeddingValue, out var vector))
            {
                cosine = CosineSimilarity(queryVector, vector);
            }
            results.Add(new RetrievedSpan(
                document.GetString("documentId")!,
                document.GetString("sectionId")!,
                document.GetString("spanId")!,
                document.GetString("text")!,
                document.TryGetValue("pageNumber", out int page) ? page : 0,
                cosine));
        }

        return Deduplicate(results).Take(top).ToList();
    }

    private static IEnumerable<RetrievedSpan> Deduplicate(IEnumerable<RetrievedSpan> spans)
    {
        return spans
            .GroupBy(span => span.SpanId)
            .Select(group => group.OrderByDescending(span => span.Score).First());
    }

    private static bool TryConvertEmbedding(object? value, out IReadOnlyList<float> embedding)
    {
        switch (value)
        {
            case float[] floatArray:
                embedding = floatArray;
                return true;
            case IEnumerable<float> floatEnumerable:
                embedding = floatEnumerable.ToArray();
                return true;
            case double[] doubleArray:
                embedding = doubleArray.Select(static d => (float)d).ToArray();
                return true;
            case IEnumerable<double> doubleEnumerable:
                embedding = doubleEnumerable.Select(static d => (float)d).ToArray();
                return true;
            default:
                embedding = Array.Empty<float>();
                return false;
        }
    }

    private static double CosineSimilarity(IReadOnlyList<float> query, IReadOnlyList<float> document)
    {
        var length = Math.Min(query.Count, document.Count);
        double dot = 0;
        double queryNorm = 0;
        double docNorm = 0;
        for (var i = 0; i < length; i++)
        {
            var q = query[i];
            var d = document[i];
            dot += q * d;
            queryNorm += q * q;
            docNorm += d * d;
        }

        if (queryNorm == 0 || docNorm == 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(queryNorm) * Math.Sqrt(docNorm));
    }
}

public sealed record RetrievedSpan(
    string DocumentId,
    string SectionId,
    string SpanId,
    string Text,
    int PageNumber,
    double Score);
