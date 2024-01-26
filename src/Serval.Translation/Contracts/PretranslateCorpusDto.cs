namespace Serval.Translation.Contracts;

public class PretranslateCorpusDto
{
    public ResourceLinkDto Corpus { get; set; } = default!;

    public IList<string>? TextIds { get; set; }

    public string? BiblicalRange { get; set; }
}
