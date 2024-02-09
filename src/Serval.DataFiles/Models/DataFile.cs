namespace Serval.DataFiles.Models;

public record DataFile : IOwnedEntity
{
    public string Id { get; set; } = "";
    public int Revision { get; set; } = 1;
    public required string Owner { get; init; }
    public required string Name { get; init; }
    public string Filename { get; init; } = "";
    public required FileFormat Format { get; init; }
}
