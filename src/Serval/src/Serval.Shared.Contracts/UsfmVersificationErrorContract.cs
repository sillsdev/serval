namespace Serval.Shared.Contracts;

public enum UsfmVersificationErrorType
{
    MissingChapter,
    MissingVerse,
    ExtraVerse,
    InvalidVerseRange,
    MissingVerseSegment,
    ExtraVerseSegment,
    InvalidChapterNumber,
    InvalidVerseNumber,
}

public record UsfmVersificationErrorContract
{
    public required UsfmVersificationErrorType Type { get; init; }
    public required string ProjectName { get; init; }
    public required string ExpectedVerseRef { get; init; }
    public required string ActualVerseRef { get; init; }
}
