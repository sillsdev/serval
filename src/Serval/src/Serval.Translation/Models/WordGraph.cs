namespace Serval.Translation.Models;

public record WordGraph
{
    public required IReadOnlyList<string> SourceTokens { get; set; }
    public required double InitialStateScore { get; set; }
    public required IReadOnlySet<int> FinalStates { get; set; }
    public required IReadOnlyList<WordGraphArc> Arcs { get; set; }
}
