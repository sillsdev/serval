namespace Serval.Translation.Consumers;

public class CorpusUpdatedConsumer(IEngineService engineService) : IConsumer<CorpusUpdated>
{
    private readonly IEngineService _engineService = engineService;

    public async Task Consume(ConsumeContext<CorpusUpdated> context)
    {
        await _engineService.UpdateCorpusFilesAsync(
            context.Message.CorpusId,
            context.Message.Files.Select(Map).ToList(),
            context.CancellationToken
        );
    }

    private static CorpusFile Map(CorpusFileResult corpusFile)
    {
        return new CorpusFile
        {
            Id = corpusFile.File.DataFileId,
            TextId = corpusFile.TextId,
            Filename = corpusFile.File.Filename,
            Format = corpusFile.File.Format,
        };
    }
}
