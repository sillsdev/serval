namespace Serval.Translation.Models;

public class Pretranslation : IEntity
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = default!;
    public int Revision { get; set; } = 1;
    public string EngineRef { get; set; } = default!;
    public int ModelRevision { get; set; }
    public string CorpusRef { get; set; } = default!;
    public string TextId { get; set; } = default!;
    public List<string> Refs { get; set; } = default!;
    public string Translation { get; set; } = default!;
}
