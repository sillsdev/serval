namespace Serval.Translation.Contracts;

public class TranslationEngineDto
{
    public string Id { get; set; } = default!;
    public string Url { get; set; } = default!;
    public string? Name { get; set; }
    public string SourceLanguage { get; set; } = default!;
    public string TargetLanguage { get; set; } = default!;
    public string Type { get; set; } = default!;
    public bool IsModelPersisted { get; set; } = false;
    public bool IsBuilding { get; set; }
    public int ModelRevision { get; set; }
    public double Confidence { get; set; }
    public int CorpusSize { get; set; }
}
