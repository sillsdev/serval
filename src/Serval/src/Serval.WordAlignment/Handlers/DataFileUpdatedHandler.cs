namespace Serval.WordAlignment.Handlers;

public class DataFileUpdatedHandler(
    IDataAccessContext dataAccessContext,
    IRepository<Engine> engines,
    IRepository<Models.WordAlignment> wordAlignments
) : IEventHandler<DataFileUpdated>
{
    public async Task HandleAsync(DataFileUpdated evt, CancellationToken cancellationToken)
    {
        await dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                await engines.UpdateAllAsync(
                    e =>
                        e.ParallelCorpora.Any(c =>
                            c.SourceCorpora.Any(cs => cs.Files.Any(f => f.Id == evt.DataFileId))
                            || c.TargetCorpora.Any(tc => tc.Files.Any(f => f.Id == evt.DataFileId))
                        ),
                    u =>
                    {
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
                    cancellationToken: cancellationToken
                );

                HashSet<string> parallelCorpusIds = (
                    await engines.GetAllAsync(
                        e =>
                            e.ParallelCorpora.Any(c =>
                                c.SourceCorpora.Any(cs => cs.Files.Any(f => f.Id == evt.DataFileId))
                                || c.TargetCorpora.Any(tc => tc.Files.Any(f => f.Id == evt.DataFileId))
                            ),
                        cancellationToken: cancellationToken
                    )
                )
                    .SelectMany(e => e.ParallelCorpora.Select(c => c.Id))
                    .ToHashSet();

                await wordAlignments.DeleteAllAsync(
                    wa => parallelCorpusIds.Contains(wa.CorpusRef),
                    cancellationToken: cancellationToken
                );
            },
            cancellationToken: cancellationToken
        );
    }
}
