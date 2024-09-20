namespace Serval.Translation.Contracts;

public record TranslationBuildConfigDto
{
    public string? Name { get; init; }
    public IReadOnlyList<CorpusFilterConfigDto>? TrainOn { get; init; }
    public IReadOnlyList<PretranslateCorpusConfigDto>? Pretranslate { get; init; }

    /// <example>
    /// {
    ///   "property" : "value"
    /// }
    /// </example>
    public object? Options { get; init; }
}
