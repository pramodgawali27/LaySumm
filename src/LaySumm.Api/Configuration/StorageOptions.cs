using System.ComponentModel.DataAnnotations;

namespace LaySumm.Api.Configuration;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    [Required]
    public required string ConnectionString { get; init; }

    [Required]
    public required string ContainerName { get; init; }
}
