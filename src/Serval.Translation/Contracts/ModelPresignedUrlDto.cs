namespace Serval.Translation.Models;

public class ModelPresignedUrlDto
{
    public string PresignedUrl { get; set; } = default!;
    public string BuildRevision { get; set; } = default!;
}
