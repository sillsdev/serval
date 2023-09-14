using System.Text.Json.Nodes;

namespace Serval.Translation.Contracts;

public class TranslationBuildConfigDto
{
    public IList<PretranslateCorpusConfigDto>? Pretranslate { get; set; }

    public string? Options { get; set; }
}
