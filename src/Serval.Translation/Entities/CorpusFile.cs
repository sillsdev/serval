namespace Serval.Translation.Entities;

public class CorpusFile
{
    public string Id { get; set; } = default!;
    public string DataFileRef { get; set; } = default!;
    public string LanguageTag { get; set; } = default!;
    public string? TextId { get; set; }
}
