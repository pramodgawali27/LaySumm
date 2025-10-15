using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using LaySumm.Api.Configuration;
using LaySumm.Api.Processing.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SearchOptionsConfig = LaySumm.Api.Configuration.SearchOptions;

namespace LaySumm.Api.Processing.Indexing;

public sealed class DocumentIndexer
{
    private readonly SearchClient _searchClient;
    private readonly OpenAIClient _azureClient;
    private readonly AzureOpenAIOptions _azureOptions;
    private readonly SearchOptionsConfig _searchOptions;
    private readonly ILogger<DocumentIndexer> _logger;

    public DocumentIndexer(
        SearchClient searchClient,
        IOptions<AzureOpenAIOptions> azureOptions,
        IOptions<SearchOptionsConfig> searchOptions,
        ILogger<DocumentIndexer> logger)
    {
        _searchClient = searchClient;
        _azureOptions = azureOptions.Value;
        _searchOptions = searchOptions.Value;
        _azureClient = new OpenAIClient(new Uri(_azureOptions.Endpoint), new AzureKeyCredential(_azureOptions.Key));
        _logger = logger;
    }

    public async Task EnsureIndexAsync(CancellationToken cancellationToken)
    {
        try
        {
            var definition = new SearchIndex(_searchClient.IndexName)
            {
                Fields = new List<SearchField>
                {
                    new SimpleField("id", SearchFieldDataType.String) { IsKey = true },
                    new SimpleField("documentId", SearchFieldDataType.String) { IsFilterable = true },
                    new SimpleField("sectionId", SearchFieldDataType.String) { IsFilterable = true },
                    new SimpleField("spanId", SearchFieldDataType.String) { IsFilterable = true },
                    new SearchableField("text") { AnalyzerName = LexicalAnalyzerName.EnLucene },
                    new SimpleField("pageNumber", SearchFieldDataType.Int32) { IsFilterable = true },
                    new SearchField("embedding", SearchFieldDataType.Collection(SearchFieldDataType.Single))
                    {
                        IsSearchable = true,
                        VectorSearchDimensions = 1536,
                        VectorSearchProfileName = "openai-default"
                    }
                }
            };

            var adminClient = new SearchIndexClient(new Uri(_searchOptions.Endpoint), new AzureKeyCredential(_searchOptions.Key));
            await adminClient.CreateOrUpdateIndexAsync(definition, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ensure Azure Search index exists. Assuming it already exists.");
        }
    }

    public async Task IndexAsync(StructuredDocument document, CancellationToken cancellationToken)
    {
        var actions = new List<IndexDocumentsAction<SearchDocument>>();

        foreach (var section in document.Sections)
        {
            foreach (var span in section.Spans)
            {
                var embedding = await EmbedAsync(span.Text, cancellationToken);
                var searchDocument = new SearchDocument
                {
                    ["id"] = $"{document.DocumentId}-{span.SpanId}",
                    ["documentId"] = document.DocumentId,
                    ["sectionId"] = section.SectionId,
                    ["spanId"] = span.SpanId,
                    ["text"] = span.Text,
                    ["pageNumber"] = span.PageNumber,
                    ["embedding"] = embedding
                };

                actions.Add(IndexDocumentsAction.Upload(searchDocument));
            }
        }

        if (actions.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Uploading {Count} spans to Azure AI Search for document {DocumentId}", actions.Count, document.DocumentId);
        var batch = IndexDocumentsBatch.Create(actions.ToArray());
        await _searchClient.IndexDocumentsAsync(batch, cancellationToken: cancellationToken);
    }

    private async Task<IReadOnlyList<float>> EmbedAsync(string text, CancellationToken cancellationToken)
    {
        var options = new EmbeddingsOptions(_azureOptions.Deployment, new[] { text });
        var embedding = await _azureClient.GetEmbeddingsAsync(options, cancellationToken);
        return embedding.Value.Data.First().Embedding.ToArray();
    }
}
