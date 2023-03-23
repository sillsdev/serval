namespace Serval.DataFiles.Models;

public class DataFile : IOwnedEntity
{
    public string Id { get; set; } = default!;
    public int Revision { get; set; } = 1;
    public string Owner { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string Filename { get; set; } = default!;
    public DataFileFormat Format { get; set; }
}
