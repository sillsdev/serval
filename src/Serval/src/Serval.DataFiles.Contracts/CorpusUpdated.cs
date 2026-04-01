namespace Serval.DataFiles.Contracts;

public record CorpusUpdated(string CorpusId, IReadOnlyList<CorpusDataFileContract> Files) : IEvent;
