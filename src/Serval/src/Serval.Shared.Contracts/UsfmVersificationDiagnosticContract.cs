namespace Serval.Shared.Contracts;

public enum UsfmVersificationDiagnosticType
{
    Missing,
    Extra,
    Invalid,
    IncorrectVerseSegment,
    UnsupportedVerseRange,
}

public record UsfmVersificationDiagnosticContract
{
    public required UsfmVersificationDiagnosticType Type { get; init; }
    public required int NumAffectedVerses { get; init; }
    public required IReadOnlyList<string> References { get; init; }
    public required string Filename { get; init; }
    public required IReadOnlyList<int> LineNumbers { get; init; }
}
