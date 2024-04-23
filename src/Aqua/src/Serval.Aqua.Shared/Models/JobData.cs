namespace Serval.Aqua.Shared.Models;

public record JobData
{
    public required CorpusData CorpusData { get; init; }
    public CorpusData? ReferenceCorpusData { get; init; }
}
