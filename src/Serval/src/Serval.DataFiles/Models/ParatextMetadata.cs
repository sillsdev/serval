namespace Serval.DataFiles.Models;

public record ParatextMetadata
{
    public required string ProjectGuid { get; init; } = "";
    public required string ProjectName { get; init; } = "";
}
