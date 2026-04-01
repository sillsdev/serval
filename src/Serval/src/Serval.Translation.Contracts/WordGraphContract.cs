namespace Serval.Translation.Contracts;

public record WordGraphContract
{
    public required IReadOnlyList<string> SourceTokens { get; set; }
    public required double InitialStateScore { get; set; }
    public required IReadOnlySet<int> FinalStates { get; set; }
    public required IReadOnlyList<WordGraphArcContract> Arcs { get; set; }
}
