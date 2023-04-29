namespace Serval.Translation.Contracts;

public class WordGraphDto
{
    public List<string> SourceWords { get; set; } = default!;
    public float InitialStateScore { get; set; }
    public List<int> FinalStates { get; set; } = default!;
    public List<WordGraphArcDto> Arcs { get; set; } = default!;
}
