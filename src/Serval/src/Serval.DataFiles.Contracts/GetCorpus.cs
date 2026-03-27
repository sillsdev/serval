namespace Serval.DataFiles.Contracts;

public record GetCorpus(string CorpusId, string Owner) : IRequest<GetCorpusResponse>;
