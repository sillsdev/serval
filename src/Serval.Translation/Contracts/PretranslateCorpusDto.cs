namespace Serval.Translation.Contracts;

public class PretranslateCorpusDto
{
    public ResourceLinkDto Corpus { get; set; } = default!;

    public List<string>? TextIds { get; set; }
}
