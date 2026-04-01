namespace Serval.WordAlignment.Handlers;

public class CorpusUpdatedHandler(IEngineService engineService) : IEventHandler<CorpusUpdated>
{
    public async Task HandleAsync(CorpusUpdated evt, CancellationToken cancellationToken)
    {
        await engineService.UpdateCorpusFilesAsync(evt.CorpusId, [.. evt.Files.Select(Map)], cancellationToken);
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
