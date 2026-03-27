namespace Serval.DataFiles.Handlers;

public class GetCorpusHandler(ICorpusService corpusService, IDataFileService dataFileService)
    : IRequestHandler<GetCorpus, GetCorpusResponse>
{
    public async Task<GetCorpusResponse> HandleAsync(GetCorpus request, CancellationToken cancellationToken)
    {
        try
        {
            Corpus corpus = await corpusService.GetAsync(request.CorpusId, request.Owner, cancellationToken);
            IEnumerable<string> corpusFileIds = corpus.Files.Select(f => f.FileRef);
            var corpusDataFilesDict = (
                await dataFileService.GetAllAsync(corpusFileIds, cancellationToken)
            ).ToDictionary(f => f.Id);
            return new GetCorpusResponse(
                IsFound: true,
                new CorpusView(
                    corpus.Id,
                    corpus.Language,
                    corpus.Name,
                    [
                        .. corpus.Files.Select(f => new CorpusFileView(
                            File: Map(corpusDataFilesDict[f.FileRef]),
                            f.TextId ?? corpusDataFilesDict[f.FileRef].Name
                        )),
                    ]
                )
            );
        }
        catch (EntityNotFoundException)
        {
            return new GetCorpusResponse(IsFound: false);
        }
    }

    private static DataFileView Map(DataFile dataFile)
    {
        return new DataFileView(dataFile.Id, dataFile.Name, dataFile.Filename, dataFile.Format);
    }
}
