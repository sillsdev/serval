namespace Serval.Translation.Handlers;

public class DataFileDeletedHandler(
    IDataAccessContext dataAccessContext,
    IRepository<Engine> engines,
    IRepository<Pretranslation> pretranslations
) : IEventHandler<DataFileDeleted>
{
    public async Task HandleAsync(DataFileDeleted evt, CancellationToken cancellationToken)
    {
        HashSet<string> parallelCorpusIds = [];
        HashSet<string> corpusIds = [];
        await dataAccessContext.WithTransactionAsync(
            async ct =>
            {
                parallelCorpusIds =
                [
                    .. (
                        await engines.GetAllAsync(
                            e =>
                                e.ParallelCorpora.Any(c =>
                                    c.SourceCorpora.Any(cs => cs.Files.Any(f => f.Id == evt.DataFileId))
                                    || c.TargetCorpora.Any(tc => tc.Files.Any(f => f.Id == evt.DataFileId))
                                ),
                            cancellationToken: ct
                        )
                    ).SelectMany(e =>
                        e.ParallelCorpora.Where(c =>
                                c.SourceCorpora.Any(cs => cs.Files.Any(f => f.Id == evt.DataFileId))
                                || c.TargetCorpora.Any(tc => tc.Files.Any(f => f.Id == evt.DataFileId))
                            )
                            .Select(c => c.Id)
                    ),
                ];

                corpusIds =
                [
                    .. (
                        await engines.GetAllAsync(
                            e =>
                                e.Corpora.Any(c =>
                                    c.SourceFiles.Any(f => f.Id == evt.DataFileId)
                                    || c.TargetFiles.Any(f => f.Id == evt.DataFileId)
                                ),
                            cancellationToken: ct
                        )
                    ).SelectMany(e =>
                        e.Corpora.Where(c =>
                                c.SourceFiles.Any(f => f.Id == evt.DataFileId)
                                || c.TargetFiles.Any(f => f.Id == evt.DataFileId)
                            )
                            .Select(c => c.Id)
                    ),
                ];

                await engines.UpdateAllAsync(
                    e =>
                        e.Corpora.Any(c =>
                            c.SourceFiles.Any(f => f.Id == evt.DataFileId)
                            || c.TargetFiles.Any(f => f.Id == evt.DataFileId)
                        )
                        || e.ParallelCorpora.Any(c =>
                            c.SourceCorpora.Any(sc => sc.Files.Any(f => f.Id == evt.DataFileId))
                            || c.TargetCorpora.Any(tc => tc.Files.Any(f => f.Id == evt.DataFileId))
                        ),
                    u =>
                    {
                        u.RemoveAll(e => e.Corpora.AllElements().SourceFiles, f => f.Id == evt.DataFileId);
                        u.RemoveAll(e => e.Corpora.AllElements().TargetFiles, f => f.Id == evt.DataFileId);
                        u.RemoveAll(
                            e => e.ParallelCorpora.AllElements().SourceCorpora.AllElements().Files,
                            f => f.Id == evt.DataFileId
                        );
                        u.RemoveAll(
                            e => e.ParallelCorpora.AllElements().TargetCorpora.AllElements().Files,
                            f => f.Id == evt.DataFileId
                        );
                    },
                    cancellationToken: ct
                );
            },
            cancellationToken
        );

        cancellationToken.ThrowIfCancellationRequested();
        await pretranslations.DeleteAllAsync(
            pt => parallelCorpusIds.Contains(pt.CorpusRef) || corpusIds.Contains(pt.CorpusRef),
            batchSize: 1000,
            cancellationToken: cancellationToken
        );
    }
}
