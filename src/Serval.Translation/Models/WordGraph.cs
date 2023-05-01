namespace Serval.Translation.Models;

public class WordGraph
{
    public List<string> SourceTokens { get; set; } = default!;
    public double InitialStateScore { get; set; }
    public HashSet<int> FinalStates { get; set; } = default!;
    public List<WordGraphArc> Arcs { get; set; } = default!;
}
