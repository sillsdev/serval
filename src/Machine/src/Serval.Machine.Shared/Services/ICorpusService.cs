﻿namespace Serval.Machine.Shared.Services;

public interface ICorpusService
{
    IEnumerable<ITextCorpus> CreateTextCorpora(IReadOnlyList<CorpusFile> files);
    IEnumerable<ITextCorpus> CreateTermCorpora(IReadOnlyList<CorpusFile> files);
}
