namespace Serval.Translation.Contracts;

public class TrainingCorpusConfigDto
{
    public string CorpusId { get; set; } = default!;
    public IList<string>? TextIds { get; set; }
}
