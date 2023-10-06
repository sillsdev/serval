namespace Serval.Translation.Contracts;

public class TranslationBuildConfigDto
{
    public string? Name { get; set; }
    public IList<PretranslateCorpusConfigDto>? Pretranslate { get; set; }

    public string? Options { get; set; }
}
