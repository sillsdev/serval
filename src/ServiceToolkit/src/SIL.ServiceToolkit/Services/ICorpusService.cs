﻿namespace SIL.ServiceToolkit.Services;

public interface ICorpusService
{
    IEnumerable<ITextCorpus> CreateTextCorpora(IReadOnlyList<CorpusFile> files);
    IEnumerable<ITextCorpus> CreateTermCorpora(
        IReadOnlyList<(CorpusFile File, Dictionary<string, HashSet<int>>? Chapters)> files
    );
}