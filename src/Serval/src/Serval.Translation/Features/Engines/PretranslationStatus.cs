namespace Serval.Translation.Features.Engines;

public enum PretranslationStatus
{
    Found,
    CorpusNotFound,
    NotBuilt,
}

public static class EngineExtensions
{
    public static PretranslationStatus GetCorpusPretranslationStatus(this Engine engine, string corpusId)
    {
        if (!engine.Corpora.Any(c => c.Id == corpusId) && !engine.ParallelCorpora.Any(c => c.Id == corpusId))
            return PretranslationStatus.CorpusNotFound;
        if (engine.ModelRevision == 0)
            return PretranslationStatus.NotBuilt;
        return PretranslationStatus.Found;
    }

    public static PretranslationStatus GetParallelCorpusPretranslationStatus(
        this Engine engine,
        string parallelCorpusId
    )
    {
        if (!engine.ParallelCorpora.Any(c => c.Id == parallelCorpusId))
            return PretranslationStatus.CorpusNotFound;
        if (engine.ModelRevision == 0)
            return PretranslationStatus.NotBuilt;
        return PretranslationStatus.Found;
    }
}
