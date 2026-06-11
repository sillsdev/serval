namespace Serval.WordAlignment.Handlers;

public class CorpusUpdatedHandler(IRepository<Engine> engines, IRepository<Models.WordAlignment> wordAlignments)
    : IEventHandler<CorpusUpdated>
{
    public async Task HandleAsync(CorpusUpdated evt, CancellationToken cancellationToken)
    {
        IReadOnlyList<CorpusFile> files = [.. evt.Files.Select(Map)];
        await engines.UpdateAllAsync(
            e =>
                e.ParallelCorpora.Any(c =>
                    c.SourceCorpora.Any(sc => sc.Id == evt.CorpusId) || c.TargetCorpora.Any(tc => tc.Id == evt.CorpusId)
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
            cancellationToken: cancellationToken
        );

        await wordAlignments.DeleteAllAsync(wa => wa.CorpusRef == evt.CorpusId, cancellationToken: cancellationToken);
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
