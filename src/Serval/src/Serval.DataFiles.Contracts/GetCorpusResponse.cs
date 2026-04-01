using System.Diagnostics.CodeAnalysis;

namespace Serval.DataFiles.Contracts;

public record GetCorpusResponse(
    [property: MemberNotNullWhen(true, nameof(GetCorpusResponse.Corpus))] bool IsFound,
    CorpusContract? Corpus = null
);
