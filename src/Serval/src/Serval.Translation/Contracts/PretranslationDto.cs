namespace Serval.Translation.Contracts;

public record PretranslationDto
{
    public required string TextId { get; init; }
    public required IReadOnlyList<string> SourceRefs { get; init; }
    public required IReadOnlyList<string> TargetRefs { get; init; }

    [Obsolete]
    public IReadOnlyList<string>? Refs { get; init; }
    public required string Translation { get; init; }
}
