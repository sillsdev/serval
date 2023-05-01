namespace Serval.Translation.Contracts;

public class WordGraphDto
{
    public IList<string> SourceTokens { get; set; } = default!;
    public float InitialStateScore { get; set; }
    public ISet<int> FinalStates { get; set; } = default!;
    public IList<WordGraphArcDto> Arcs { get; set; } = default!;
}
