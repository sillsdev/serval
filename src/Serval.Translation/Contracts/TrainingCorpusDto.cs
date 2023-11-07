namespace Serval.Translation.Contracts;

public class TrainingCorpusDto
{
    public ResourceLinkDto Corpus { get; set; } = default!;

    public IList<string>? TextIds { get; set; }
}
