namespace SIL.DataAccess;

public interface IEntity
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    string Id { get; set; }
    int Revision { get; set; }
}
