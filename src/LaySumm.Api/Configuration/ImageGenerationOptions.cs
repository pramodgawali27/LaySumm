using System.ComponentModel.DataAnnotations;

namespace LaySumm.Api.Configuration;

public sealed class ImageGenerationOptions
{
    public const string SectionName = "ImageGeneration";

    public ImageProvider Provider { get; init; } = ImageProvider.None;

    public OpenAiImageOptions? OpenAI { get; init; }

    public AzureOpenAiImageOptions? AzureOpenAI { get; init; }
}

public enum ImageProvider
{
    None = 0,
    OpenAI = 1,
    AzureOpenAI = 2
}

public sealed class OpenAiImageOptions
{
    [Required]
    public required string ApiKey { get; init; }

    public string? Organization { get; init; }
}

public sealed class AzureOpenAiImageOptions
{
    [Required]
    public required string Endpoint { get; init; }

    [Required]
    public required string Key { get; init; }

    [Required]
    public required string Deployment { get; init; }
}
