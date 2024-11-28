namespace Serval.DataFiles.Contracts;

public record DataFileReferenceDto
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public string? Name { get; init; }
    public required FileFormat Format { get; init; }
}
