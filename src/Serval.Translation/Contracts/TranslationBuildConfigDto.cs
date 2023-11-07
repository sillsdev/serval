namespace Serval.Translation.Contracts;

public class TranslationBuildConfigDto
{
    public string? Name { get; set; }
    public IList<TrainingCorpusConfigDto>? TrainOn { get; set; }
    public IList<PretranslateCorpusConfigDto>? Pretranslate { get; set; }

    /// <example>
    /// {
    ///   "property" : "value"
    /// }
    /// </example>
    public object? Options { get; set; }
}
