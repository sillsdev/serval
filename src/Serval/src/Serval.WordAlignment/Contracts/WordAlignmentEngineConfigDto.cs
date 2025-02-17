namespace Serval.WordAlignment.Contracts;

public record WordAlignmentEngineConfigDto
{
    /// <summary>
    /// The word alignment engine name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The source language tag.
    /// </summary>
    public required string SourceLanguage { get; init; }

    /// <summary>
    /// The target language tag.
    /// </summary>
    public required string TargetLanguage { get; init; }

    /// <summary>
    /// The translation engine type.
    /// </summary>
    public required string Type { get; init; }
}
