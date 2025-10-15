using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.AI.OpenAI;
using Azure.Storage.Blobs;
using LaySumm.Api.Configuration;
using LaySumm.Api.Processing.Models;
using LaySumm.Api.Processing.Summarization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Images;
using OpenAiSdkClient = OpenAI.OpenAIClient;
using AzureOpenAIClient = Azure.AI.OpenAI.OpenAIClient;
using AzureImageGenerationOptions = Azure.AI.OpenAI.ImageGenerationOptions;
using AzureImageSize = Azure.AI.OpenAI.ImageSize;
using AzureImageResponseFormat = Azure.AI.OpenAI.ImageGenerationResponseFormat;

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
        IOptions<ImageGenerationOptions> imageGenerationOptions,
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

    private static IImageGenerator? CreateImageGenerator(ImageGenerationOptions options)
    {
        return options.Provider switch
        {
            ImageProvider.OpenAI when options.OpenAI is not null => new OpenAiImageGenerator(options.OpenAI.ApiKey, options.OpenAI.Organization),
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
            logger.LogInformation("Image generation configured via OPENAI_API_KEY environment variable.");
            return new OpenAiImageGenerator(openAiKey, organization: null);
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

    private sealed class OpenAiImageGenerator : IImageGenerator
    {
        private readonly OpenAiSdkClient _client;

        public OpenAiImageGenerator(string apiKey, string? organization)
        {
            if (!string.IsNullOrWhiteSpace(organization))
            {
                _client = new OpenAiSdkClient(apiKey, organization);
            }
            else
            {
                _client = new OpenAiSdkClient(apiKey);
            }
        }

        public async Task<string?> GenerateAsync(string prompt, CancellationToken cancellationToken)
        {
            var request = new ImageGenerationRequest
            {
                Prompt = prompt,
                Size = ImageSize._1024x1024,
                ResponseFormat = ImageResponseFormat.B64Json
            };

            var response = await _client.ImagesEndPoint.GenerateImageAsync(request, cancellationToken);
            return response.Data.FirstOrDefault()?.B64Json;
        }
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
                Prompt = prompt,
                Size = new AzureImageSize("1024x1024"),
                ResponseFormat = AzureImageResponseFormat.Base64
            };

            var response = await _client.GetImageGenerationsAsync(_deployment, options, cancellationToken);
            return response.Value.Data.FirstOrDefault()?.Base64Data;
        }
    }
}
