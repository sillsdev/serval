namespace Serval.Translation.Contracts;

public class WordGraphDto
{
    public float InitialStateScore { get; set; }
    public int[] FinalStates { get; set; } = default!;
    public WordGraphArcDto[] Arcs { get; set; } = default!;
}
