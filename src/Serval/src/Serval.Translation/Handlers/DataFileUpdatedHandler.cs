namespace Serval.Translation.Handlers;

public class DataFileUpdatedHandler(
    IDataAccessContext dataAccessContext,
    IRepository<Engine> engines,
    IRepository<Pretranslation> pretranslations
) : IEventHandler<DataFileUpdated>
{
    public async Task HandleAsync(DataFileUpdated evt, CancellationToken cancellationToken)
    {
        HashSet<string> parallelCorpusIds = [];
        HashSet<string> corpusIds = [];
        await dataAccessContext.WithTransactionAsync(
            async ct =>
            {
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
                        u.SetAll(
                            e => e.Corpora.AllElements().SourceFiles,
                            f => f.Filename,
                            evt.Filename,
                            f => f.Id == evt.DataFileId
                        );
                        u.SetAll(
                            e => e.Corpora.AllElements().TargetFiles,
                            f => f.Filename,
                            evt.Filename,
                            f => f.Id == evt.DataFileId
                        );
                        u.SetAll(
                            e => e.ParallelCorpora.AllElements().SourceCorpora.AllElements().Files,
                            f => f.Filename,
                            evt.Filename,
                            f => f.Id == evt.DataFileId
                        );
                        u.SetAll(
                            e => e.ParallelCorpora.AllElements().TargetCorpora.AllElements().Files,
                            f => f.Filename,
                            evt.Filename,
                            f => f.Id == evt.DataFileId
                        );
                    },
                    cancellationToken: ct
                );

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
                    ).SelectMany(e => e.ParallelCorpora.Select(c => c.Id)),
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
                    ).SelectMany(e => e.Corpora.Select(c => c.Id)),
                ];
            },
            cancellationToken: cancellationToken
        );

        cancellationToken.ThrowIfCancellationRequested();
        await pretranslations.DeleteAllAsync(
            pt => parallelCorpusIds.Contains(pt.CorpusRef) || corpusIds.Contains(pt.CorpusRef),
            cancellationToken: cancellationToken
        );
    }
}
