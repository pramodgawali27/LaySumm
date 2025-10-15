using System.ComponentModel.DataAnnotations;

namespace LaySumm.Api.Configuration;

public sealed class SearchOptions
{
    public const string SectionName = "Search";

    [Required]
    public required string Endpoint { get; init; }

    [Required]
    public required string IndexName { get; init; }

    [Required]
    public required string Key { get; init; }
}
