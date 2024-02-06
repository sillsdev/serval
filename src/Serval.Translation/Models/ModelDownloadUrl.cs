namespace Serval.Translation.Models;

public class ModelDownloadUrl
{
    public string Url { get; set; } = default!;
    public string ModelRevision { get; set; } = default!;
    public DateTime ExpiresAt { get; set; } = default!;
}
