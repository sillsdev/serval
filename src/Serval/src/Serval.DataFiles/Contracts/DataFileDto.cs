namespace Serval.DataFiles.Contracts;

public record DataFileDto
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public string? Name { get; init; }
    public required FileFormat Format { get; init; }
    public required int Revision { get; init; }
}
