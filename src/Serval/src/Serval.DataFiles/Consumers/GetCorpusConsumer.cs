namespace Serval.DataFiles.Consumers;

public class GetCorpusConsumer(ICorpusService corpusService) : IConsumer<GetCorpus>
{
    private readonly ICorpusService _corpusService = corpusService;

    public async Task Consume(ConsumeContext<GetCorpus> context)
    {
        try
        {
            Corpus corpus = await _corpusService.GetAsync(
                context.Message.CorpusId,
                context.Message.Owner,
                context.CancellationToken
            );
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
                            File = new DataFileResult
                            {
                                DataFileId = f.File.Id,
                                Filename = f.File.Filename,
                                Format = f.File.Format,
                                Name = f.File.Name
                            }
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
}
