namespace Serval.Translation.Handlers;

public class CorpusUpdatedHandler(IEngineService engineService) : IEventHandler<CorpusUpdated>
{
    public Task HandleAsync(CorpusUpdated evt, CancellationToken cancellationToken)
    {
        return engineService.UpdateCorpusFilesAsync(evt.CorpusId, [.. evt.Files.Select(Map)], cancellationToken);
    }

    private static CorpusFile Map(CorpusFileView corpusFile)
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
