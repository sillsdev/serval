namespace Serval.Translation.Contracts;

public record ModelDownloadUrlContract
{
    public required string Url { get; init; }
    public required int ModelRevision { get; init; }
    public required DateTime ExpiresAt { get; init; }
}
