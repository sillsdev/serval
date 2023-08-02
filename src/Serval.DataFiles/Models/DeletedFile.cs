namespace Serval.DataFiles.Models;

public class DeletedFile : IEntity
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = default!;
    public int Revision { get; set; } = 1;
    public string Filename { get; set; } = default!;
    public DateTime DeletedAt { get; set; }
}
