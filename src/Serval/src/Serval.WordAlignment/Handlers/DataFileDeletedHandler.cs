namespace Serval.WordAlignment.Handlers;

public class DataFileDeletedHandler(
    IDataAccessContext dataAccessContext,
    IRepository<Engine> engines,
    IRepository<Models.WordAlignment> wordAlignments
) : IEventHandler<DataFileDeleted>
{
    public async Task HandleAsync(DataFileDeleted evt, CancellationToken cancellationToken)
    {
        string dataFileId = evt.DataFileId;
        await dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                HashSet<string> parallelCorpusIds = (
                    await engines.GetAllAsync(
                        e =>
                            e.ParallelCorpora.Any(c =>
                                c.SourceCorpora.Any(cs => cs.Files.Any(f => f.Id == dataFileId))
                                || c.TargetCorpora.Any(tc => tc.Files.Any(f => f.Id == dataFileId))
                            ),
                        cancellationToken: cancellationToken
                    )
                )
                    .SelectMany(e => e.ParallelCorpora.Select(c => c.Id))
                    .ToHashSet();

                await engines.UpdateAllAsync(
                    e =>
                        e.ParallelCorpora.Any(c =>
                            c.SourceCorpora.Any(sc => sc.Files.Any(f => f.Id == dataFileId))
                            || c.TargetCorpora.Any(tc => tc.Files.Any(f => f.Id == dataFileId))
                        ),
                    u =>
                    {
                        u.RemoveAll(
                            e => e.ParallelCorpora.AllElements().SourceCorpora.AllElements().Files,
                            f => f.Id == dataFileId
                        );
                        u.RemoveAll(
                            e => e.ParallelCorpora.AllElements().TargetCorpora.AllElements().Files,
                            f => f.Id == dataFileId
                        );
                    },
                    cancellationToken: cancellationToken
                );

                await wordAlignments.DeleteAllAsync(
                    wa => parallelCorpusIds.Contains(wa.CorpusRef),
                    cancellationToken: cancellationToken
                );
            },
            cancellationToken: cancellationToken
        );
    }
}
