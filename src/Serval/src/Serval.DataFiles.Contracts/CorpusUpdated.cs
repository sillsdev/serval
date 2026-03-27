namespace Serval.DataFiles.Contracts;

public record CorpusUpdated(string CorpusId, IReadOnlyList<CorpusFileView> Files) : IEvent;
