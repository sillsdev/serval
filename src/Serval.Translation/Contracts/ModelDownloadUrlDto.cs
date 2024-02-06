namespace Serval.Translation.Contracts;

public class ModelDownloadUrlDto
{
    public string Url { get; set; } = default!;
    public int ModelRevision { get; set; } = default!;
    public DateTime ExpiresAt { get; set; } = default!;
}
