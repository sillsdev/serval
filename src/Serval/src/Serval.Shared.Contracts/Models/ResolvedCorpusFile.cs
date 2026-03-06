namespace Serval.Shared.Models;

public record ResolvedCorpusFile
{
    public required string Location { get; init; }
    public required FileFormat Format { get; init; }
    public required string TextId { get; init; }
}
