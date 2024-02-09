namespace Serval.Translation.Contracts;

public record TranslationEngineConfigDto
{
    /// <summary>
    /// The translation engine name.
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

    /// <summary>
    /// The model is saved when built and can be retrieved.
    /// </summary>
    public bool? IsModelPersisted { get; init; }
}
