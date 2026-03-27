namespace Serval.DataFiles.Contracts;

public record CorpusView(string CorpusId, string Language, string? Name, IReadOnlyList<CorpusFileView> Files);
