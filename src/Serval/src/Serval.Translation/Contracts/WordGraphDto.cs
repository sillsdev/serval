namespace Serval.Translation.Contracts;

public record WordGraphDto
{
    public required IReadOnlyList<string> SourceTokens { get; init; }
    public required float InitialStateScore { get; init; }
    public required ISet<int> FinalStates { get; init; }
    public required IReadOnlyList<WordGraphArcDto> Arcs { get; init; }
}
