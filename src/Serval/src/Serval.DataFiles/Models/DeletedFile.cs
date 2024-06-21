namespace Serval.DataFiles.Models;

public class DeletedFile : IEntity
{
    public string Id { get; set; } = "";
    public int Revision { get; set; } = 1;
    public required string Filename { get; init; }
    public required DateTime DeletedAt { get; init; }
}
