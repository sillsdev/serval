namespace Serval.Translation.Models;

public class WordGraph
{
    public float InitialStateScore { get; set; }
    public List<int> FinalStates { get; set; } = new List<int>();
    public List<WordGraphArc> Arcs { get; set; } = new List<WordGraphArc>();
}
