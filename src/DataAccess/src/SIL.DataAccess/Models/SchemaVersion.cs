namespace SIL.DataAccess.Models;

public class SchemaVersion : IEntity
{
    public required string Id { get; set; }
    public int Revision { get; set; }
    public required string Collection { get; set; }
    public int Version { get; set; }
}
