namespace Serval.DataFiles.Consumers;

public class GetCorpusConsumer(ICorpusService corpusService, IDataFileService dataFileService) : IConsumer<GetCorpus>
{
    private readonly ICorpusService _corpusService = corpusService;
    private readonly IDataFileService _dataFileService = dataFileService;

    public async Task Consume(ConsumeContext<GetCorpus> context)
    {
        try
        {
            Corpus corpus = await _corpusService.GetAsync(
                context.Message.CorpusId,
                context.Message.Owner,
                context.CancellationToken
            );
            IEnumerable<string> corpusFileIds = corpus.Files.Select(f => f.FileRef);
            IDictionary<string, DataFile> corpusDataFilesDict = (
                await _dataFileService.GetAllAsync(corpusFileIds, context.CancellationToken)
            ).ToDictionary(f => f.Id);

            await context.RespondAsync(
                new CorpusResult
                {
                    CorpusId = corpus.Id,
                    Name = corpus.Name,
                    Language = corpus.Language,
                    Files = corpus
                        .Files.Select(f => new CorpusFileResult
                        {
                            TextId = f.TextId!,
                            File = Map(corpusDataFilesDict[f.FileRef]!)
                        })
                        .ToList()
                }
            );
        }
        catch (EntityNotFoundException)
        {
            await context.RespondAsync(
                new CorpusNotFound { CorpusId = context.Message.CorpusId, Owner = context.Message.Owner }
            );
        }
    }

    private static DataFileResult Map(DataFile dataFile)
    {
        return new DataFileResult
        {
            DataFileId = dataFile.Id,
            Name = dataFile.Name,
            Filename = dataFile.Filename,
            Format = dataFile.Format,
        };
    }
}
