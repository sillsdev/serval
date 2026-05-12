namespace Serval.DataFiles.Handlers;

public class GetCorpusHandler(IRepository<Corpus> corpora, IRepository<DataFile> dataFiles)
    : IRequestHandler<GetCorpus, GetCorpusResponse>
{
    public async Task<GetCorpusResponse> HandleAsync(GetCorpus request, CancellationToken cancellationToken)
    {
        Corpus? corpus = await corpora.GetAsync(
            c => c.Id == request.CorpusId && c.Owner == request.Owner,
            cancellationToken
        );
        if (corpus is null)
            return new GetCorpusResponse(IsFound: false);
        HashSet<string> corpusFileIds = corpus.Files.Select(f => f.FileRef).ToHashSet();
        IDictionary<string, DataFile> corpusDataFilesDict = (
            await dataFiles.GetAllAsync(f => corpusFileIds.Contains(f.Id), cancellationToken)
        ).ToDictionary(f => f.Id);
        return new GetCorpusResponse(
            IsFound: true,
            new CorpusContract(
                corpus.Id,
                corpus.Language,
                corpus.Name,
                [
                    .. corpus.Files.Select(f => new CorpusDataFileContract(
                        File: Map(corpusDataFilesDict[f.FileRef]),
                        f.TextId ?? corpusDataFilesDict[f.FileRef].Name
                    )),
                ]
            )
        );
    }

    private static DataFileContract Map(DataFile dataFile) =>
        new(dataFile.Id, dataFile.Name, dataFile.Filename, dataFile.Format);
}
