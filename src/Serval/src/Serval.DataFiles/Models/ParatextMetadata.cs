namespace Serval.DataFiles.Models;

public record ParatextMetadata
{
    public required string ProjectGuid { get; init; }
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public required string Versification { get; init; }
    public required string TranslationType { get; init; }
    public string? LanguageCode { get; init; }
    public string? Visibility { get; init; }
}
