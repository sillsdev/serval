namespace Serval.Translation.Handlers;

public class DataFileDeletedHandler(
    IDataAccessContext dataAccessContext,
    IRepository<Engine> engines,
    IRepository<Pretranslation> pretranslations
) : IEventHandler<DataFileDeleted>
{
    public Task HandleAsync(DataFileDeleted evt, CancellationToken cancellationToken)
    {
        return dataAccessContext.WithTransactionAsync(
            async ct =>
            {
                HashSet<string> parallelCorpusIds =
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

                HashSet<string> corpusIds =
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

                await pretranslations.UpdateAllAsync(
                    pt => parallelCorpusIds.Contains(pt.CorpusRef) || corpusIds.Contains(pt.CorpusRef),
                    u => u.Set(p => p.DeletedAt, DateTime.UtcNow),
                    cancellationToken: ct
                );
            },
            cancellationToken
        );
    }
}
