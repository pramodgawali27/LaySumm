using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using Azure.Storage.Blobs;
using LaySumm.Api.Configuration;
using LaySumm.Api.Processing.Models;
using LaySumm.Api.Processing.Summarization;
using LaySumm.Api.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AzureOpenAIClient = Azure.AI.OpenAI.OpenAIClient;
using AzureImageGenerationOptions = Azure.AI.OpenAI.ImageGenerationOptions;
using AzureImageSize = Azure.AI.OpenAI.ImageSize;
using ImageGenerationConfig = LaySumm.Api.Configuration.ImageGenerationOptions;

namespace LaySumm.Api.Processing.Visualization;

public sealed class VisualizationService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly StorageOptions _storageOptions;
    private readonly ILogger<VisualizationService> _logger;
    private readonly IImageGenerator? _imageGenerator;

    public VisualizationService(
        BlobServiceClient blobServiceClient,
        IOptions<StorageOptions> storageOptions,
        IOptions<ImageGenerationConfig> imageGenerationOptions,
        ILogger<VisualizationService> logger)
    {
        _blobServiceClient = blobServiceClient;
        _storageOptions = storageOptions.Value;
        _logger = logger;

        _imageGenerator = CreateImageGenerator(imageGenerationOptions.Value)
            ?? CreateImageGeneratorFromEnvironment(logger);
    }

    public async Task<IReadOnlyList<GeneratedVisual>> GenerateAsync(
        StructuredDocument document,
        IReadOnlyList<SummarySection> sections,
        string instructions,
        CancellationToken cancellationToken)
    {
        var visuals = new List<GeneratedVisual>();
        foreach (var section in sections)
        {
            var prompt = BuildPrompt(document, section, instructions);
            var caption = $"Illustration of {section.Title}";
            var altText = $"Generated diagram explaining {section.Title} from {document.FileName}.";
            Uri? assetUri = null;

            if (_imageGenerator is not null)
            {
                try
                {
                    var imageData = await _imageGenerator.GenerateAsync(prompt, cancellationToken);
                    if (!string.IsNullOrEmpty(imageData))
                    {
                        assetUri = await UploadAsync(document.DocumentId, section.SectionId, imageData, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to generate image for section {SectionId}. Returning prompt only.", section.SectionId);
                }
            }

            visuals.Add(new GeneratedVisual
            {
                Prompt = prompt,
                Caption = caption,
                AltText = altText,
                AssetUri = assetUri
            });
        }

        return visuals;
    }

    private static IImageGenerator? CreateImageGenerator(ImageGenerationConfig options)
    {
        return options.Provider switch
        {
            ImageProvider.AzureOpenAI when options.AzureOpenAI is not null => new AzureOpenAiImageGenerator(options.AzureOpenAI.Endpoint, options.AzureOpenAI.Key, options.AzureOpenAI.Deployment),
            ImageProvider.None => null,
            _ => null
        };
    }

    private static IImageGenerator? CreateImageGeneratorFromEnvironment(ILogger logger)
    {
        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(openAiKey))
        {
            logger.LogWarning("OPENAI image generation is not currently supported by this build.");
        }

        var azureEndpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_IMAGES_ENDPOINT");
        var azureKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_IMAGES_KEY");
        var azureDeployment = Environment.GetEnvironmentVariable("AZURE_OPENAI_IMAGES_DEPLOYMENT");
        if (!string.IsNullOrWhiteSpace(azureEndpoint)
            && !string.IsNullOrWhiteSpace(azureKey)
            && !string.IsNullOrWhiteSpace(azureDeployment))
        {
            logger.LogInformation("Image generation configured via Azure OpenAI image environment variables.");
            return new AzureOpenAiImageGenerator(azureEndpoint, azureKey, azureDeployment);
        }

        logger.LogDebug("No image generation credentials found; prompts will be returned without generated assets.");
        return null;
    }

    private async Task<Uri> UploadAsync(string documentId, string sectionId, string b64Image, CancellationToken cancellationToken)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_storageOptions.ContainerName);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        var blobName = $"{documentId}/{sectionId}-{Guid.NewGuid():N}.png";
        var blobClient = containerClient.GetBlobClient(blobName);
        var imageBytes = Convert.FromBase64String(b64Image);
        await using var stream = new MemoryStream(imageBytes);
        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);
        return blobClient.Uri;
    }

    private static string BuildPrompt(StructuredDocument document, SummarySection section, string instructions)
    {
        var bulletPoints = string.Join(", ", section.Citations.Select(citation => citation));
        return $"{instructions}\nDocument: {document.FileName}\nSection: {section.Title}\nHighlights: {bulletPoints}.";
    }

    private interface IImageGenerator
    {
        Task<string?> GenerateAsync(string prompt, CancellationToken cancellationToken);
    }

    private sealed class AzureOpenAiImageGenerator : IImageGenerator
    {
        private readonly AzureOpenAIClient _client;
        private readonly string _deployment;

        public AzureOpenAiImageGenerator(string endpoint, string key, string deployment)
        {
            _client = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(key));
            _deployment = deployment;
        }

        public async Task<string?> GenerateAsync(string prompt, CancellationToken cancellationToken)
        {
            var options = new AzureImageGenerationOptions
            {
                DeploymentName = _deployment,
                Prompt = prompt,
                Size = new AzureImageSize("1024x1024"),
                ImageCount = 1
            };

            var response = await _client.GetImageGenerationsAsync(options, cancellationToken);
            var data = response.Value.Data.FirstOrDefault();
            if (data is null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(data.Base64Data))
            {
                return data.Base64Data;
            }

            if (data.Url is Uri imageUri)
            {
                using var httpClient = new HttpClient();
                var bytes = await httpClient.GetByteArrayAsync(imageUri, cancellationToken);
                return Convert.ToBase64String(bytes);
            }

            return null;
        }
    }
}
