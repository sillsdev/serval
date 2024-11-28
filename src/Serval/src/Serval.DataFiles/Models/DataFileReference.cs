namespace Serval.DataFiles.Models;

public record DataFileReference
{
    public string Id { get; set; } = "";
    public required string Name { get; init; }
    public required FileFormat Format { get; init; }
}
