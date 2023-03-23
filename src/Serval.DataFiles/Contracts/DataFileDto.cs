namespace Serval.DataFiles.Contracts;

public class DataFileDto
{
    public string Id { get; set; } = default!;
    public string Url { get; set; } = default!;
    public string? Name { get; set; }
    public DataFileFormat Format { get; set; }
}
