using System.Collections.Concurrent;
using GatewayBff.Api.Contracts;

namespace GatewayBff.Api.Workflows;

public interface IDocumentStateStore
{
    bool TryGet(string documentId, out SummaryDispatchResponse response);
    bool TryGetByIdempotencyKey(string idempotencyKey, out SummaryDispatchResponse response);
    void Save(SummaryDispatchResponse response, string? idempotencyKey);
}

internal sealed class InMemoryDocumentStateStore : IDocumentStateStore
{
    private readonly ConcurrentDictionary<string, SummaryDispatchResponse> _documents = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SummaryDispatchResponse> _idempotency = new(StringComparer.OrdinalIgnoreCase);

    public bool TryGet(string documentId, out SummaryDispatchResponse response) =>
        _documents.TryGetValue(documentId, out response!);

    public bool TryGetByIdempotencyKey(string idempotencyKey, out SummaryDispatchResponse response) =>
        _idempotency.TryGetValue(idempotencyKey, out response!);

    public void Save(SummaryDispatchResponse response, string? idempotencyKey)
    {
        _documents[response.DocumentId] = response;
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            _idempotency[idempotencyKey] = response;
        }
    }
}
