using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using LaySumm.Api.Models.Requests;
using Microsoft.Extensions.Logging;

namespace LaySumm.Api.Processing.Ingestion;

public sealed class FileIngestionService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<FileIngestionService> _logger;

    public FileIngestionService(BlobServiceClient blobServiceClient, ILogger<FileIngestionService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _logger = logger;
    }

    public async Task<IngestedFile> DownloadAsync(SourceDocument document, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(document.BlobUri, UriKind.Absolute, out var blobUri))
        {
            throw new InvalidOperationException($"Document '{document.FileName}' has an invalid blob URI.");
        }

        _logger.LogInformation("Downloading source document {FileName} from {Uri}", document.FileName, blobUri);

        var blobClient = new BlobClient(blobUri);
        await using var memoryStream = new MemoryStream();
        await blobClient.DownloadToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;

        return new IngestedFile(document.FileName, document.MediaType, memoryStream.ToArray());
    }
}

public sealed record IngestedFile(string FileName, string? MediaType, byte[] Content);
