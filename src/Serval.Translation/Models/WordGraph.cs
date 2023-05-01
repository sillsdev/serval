namespace Serval.Translation.Models;

public class WordGraph
{
    public IList<string> SourceTokens { get; set; } = default!;
    public double InitialStateScore { get; set; }
    public ISet<int> FinalStates { get; set; } = default!;
    public IList<WordGraphArc> Arcs { get; set; } = default!;
}
