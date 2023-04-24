namespace Serval.Translation.Contracts;

public class WordGraphDto
{
    public float InitialStateScore { get; set; }
    public List<int> FinalStates { get; set; } = default!;
    public List<WordGraphArcDto> Arcs { get; set; } = default!;
}
