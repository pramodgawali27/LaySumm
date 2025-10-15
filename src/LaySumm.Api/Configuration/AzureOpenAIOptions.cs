using System.ComponentModel.DataAnnotations;

namespace LaySumm.Api.Configuration;

public sealed class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    [Required]
    public required string Endpoint { get; init; }

    [Required]
    public required string Key { get; init; }

    [Required]
    public required string Deployment { get; init; }
}
