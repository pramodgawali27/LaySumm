using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using LaySumm.Api.Configuration;
using LaySumm.Api.Models.Requests;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace LaySumm.Api.Services;

public sealed class DocumentUploadService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly StorageOptions _storageOptions;

    public DocumentUploadService(BlobServiceClient blobServiceClient, IOptions<StorageOptions> storageOptions)
    {
        _blobServiceClient = blobServiceClient;
        _storageOptions = storageOptions.Value;
    }

    public async Task<SourceDocument> UploadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            throw new InvalidOperationException("File upload is required.");
        }

        var containerClient = _blobServiceClient.GetBlobContainerClient(_storageOptions.ContainerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobName = BuildBlobName(file.FileName);
        var blobClient = containerClient.GetBlobClient(blobName);

        await using var stream = file.OpenReadStream();
        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);

        if (!string.IsNullOrWhiteSpace(file.ContentType))
        {
            await blobClient.SetHttpHeadersAsync(new BlobHttpHeaders
            {
                ContentType = file.ContentType
            }, cancellationToken: cancellationToken);
        }

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = containerClient.Name,
            BlobName = blobName,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(12)
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sasUri = blobClient.GenerateSasUri(sasBuilder);

        return new SourceDocument
        {
            BlobUri = sasUri.ToString(),
            FileName = file.FileName,
            MediaType = string.IsNullOrWhiteSpace(file.ContentType) ? null : file.ContentType
        };
    }

    private static string BuildBlobName(string fileName)
    {
        var sanitized = SanitizeFileName(fileName);
        var prefix = DateTimeOffset.UtcNow.ToString("yyyyMMdd/HHmmss", CultureInfo.InvariantCulture);
        return $"uploads/{prefix}/{Guid.NewGuid():N}-{sanitized}";
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new char[fileName.Length];
        for (var i = 0; i < fileName.Length; i++)
        {
            var ch = fileName[i];
            sanitized[i] = Array.IndexOf(invalidChars, ch) >= 0 ? '_' : ch;
        }

        var result = new string(sanitized).Trim();
        return string.IsNullOrWhiteSpace(result) ? "document.bin" : result;
    }
}
