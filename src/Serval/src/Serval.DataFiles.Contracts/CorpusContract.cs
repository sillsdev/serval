namespace Serval.DataFiles.Contracts;

public record CorpusContract(
    string CorpusId,
    string Language,
    string? Name,
    IReadOnlyList<CorpusDataFileContract> Files
);
