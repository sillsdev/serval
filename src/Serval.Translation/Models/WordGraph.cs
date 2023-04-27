namespace Serval.Translation.Models;

public class WordGraph
{
    public List<string> SourceWords { get; set; } = default!;
    public double InitialStateScore { get; set; }
    public List<int> FinalStates { get; set; } = new List<int>();
    public List<WordGraphArc> Arcs { get; set; } = new List<WordGraphArc>();
}
