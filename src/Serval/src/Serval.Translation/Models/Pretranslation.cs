namespace Serval.Translation.Models;

public class Pretranslation : IBuildResult
{
    public string Id { get; set; } = "";
    public int Revision { get; set; } = 1;
    public required string EngineRef { get; init; }
    public int BuildRevision { get; init; }
    public required string CorpusRef { get; init; }
    public required string TextId { get; init; }
    public required IReadOnlyList<string> Refs { get; init; }
    public required string Translation { get; init; }
}
