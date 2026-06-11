namespace Serval.WordAlignment.Features.Engines;

public enum WordAlignmentStatus
{
    Found,
    CorpusNotFound,
    NotBuilt,
}

public static class EngineExtensions
{
    public static WordAlignmentStatus GetCorpusWordAlignmentStatus(this Engine engine, string corpusId)
    {
        if (!engine.ParallelCorpora.Any(c => c.Id == corpusId))
            return WordAlignmentStatus.CorpusNotFound;
        if (engine.ModelRevision == 0)
            return WordAlignmentStatus.NotBuilt;
        return WordAlignmentStatus.Found;
    }

    public static WordAlignmentStatus GetParallelCorpusWordAlignmentStatus(this Engine engine, string parallelCorpusId)
    {
        if (!engine.ParallelCorpora.Any(c => c.Id == parallelCorpusId))
            return WordAlignmentStatus.CorpusNotFound;
        if (engine.ModelRevision == 0)
            return WordAlignmentStatus.NotBuilt;
        return WordAlignmentStatus.Found;
    }
}
