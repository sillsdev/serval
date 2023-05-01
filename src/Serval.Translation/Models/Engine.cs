namespace Serval.Translation.Models;

public class Engine : IOwnedEntity
{
    public string Id { get; set; } = default!;
    public int Revision { get; set; } = 1;
    public string? Name { get; set; }
    public string SourceLanguage { get; set; } = default!;
    public string TargetLanguage { get; set; } = default!;
    public string Type { get; set; } = default!;
    public string Owner { get; set; } = default!;
    public List<Corpus> Corpora { get; set; } = default!;
    public bool IsBuilding { get; set; }
    public int ModelRevision { get; set; }
    public double Confidence { get; set; }
    public int CorpusSize { get; set; }
}
