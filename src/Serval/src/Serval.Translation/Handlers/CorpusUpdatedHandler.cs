namespace Serval.Translation.Handlers;

public class CorpusUpdatedHandler(
    IDataAccessContext dataAccessContext,
    IRepository<Engine> engines,
    IRepository<Pretranslation> pretranslations
) : IEventHandler<CorpusUpdated>
{
    public Task HandleAsync(CorpusUpdated evt, CancellationToken cancellationToken)
    {
        IReadOnlyList<CorpusFile> files = [.. evt.Files.Select(Map)];
        return dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                await engines.UpdateAllAsync(
                    e =>
                        e.ParallelCorpora.Any(c =>
                            c.SourceCorpora.Any(sc => sc.Id == evt.CorpusId)
                            || c.TargetCorpora.Any(tc => tc.Id == evt.CorpusId)
                        ),
                    u =>
                    {
                        u.SetAll(
                            e => e.ParallelCorpora.AllElements().SourceCorpora,
                            mc => mc.Files,
                            files,
                            mc => mc.Id == evt.CorpusId
                        );
                        u.SetAll(
                            e => e.ParallelCorpora.AllElements().TargetCorpora,
                            mc => mc.Files,
                            files,
                            mc => mc.Id == evt.CorpusId
                        );
                    },
                    cancellationToken: ct
                );
                await pretranslations.DeleteAllAsync(pt => pt.CorpusRef == evt.CorpusId, ct);
            },
            cancellationToken
        );
    }

    private static CorpusFile Map(CorpusDataFileContract corpusFile)
    {
        return new CorpusFile
        {
            Id = corpusFile.File.DataFileId,
            TextId = corpusFile.TextId ?? corpusFile.File.Name,
            Filename = corpusFile.File.Filename,
            Format = corpusFile.File.Format,
        };
    }
}
