namespace Serval.Translation.Models;

public class ModelInfoDto
{
    public string PresignedUrl { get; set; } = default!;
    public string BuildId { get; set; } = default!;
    public string CreatedOn { get; set; } = default!;
    public string ToBeDeletedOn { get; set; } = default!;
}
