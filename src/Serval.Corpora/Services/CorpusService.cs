namespace Serval.Corpora.Services;

public class CorpusService : EntityServiceBase<Corpus>, ICorpusService
{
    private readonly IOptionsMonitor<CorpusOptions> _options;
    private readonly ICorpusAggregator _corpusAggregator;
    private readonly IDataAccessContext _dataAccessContext;

    public CorpusService(
        IRepository<Corpus> corpora,
        IOptionsMonitor<CorpusOptions> options,
        ICorpusAggregator corpusAggregator,
        IDataAccessContext dataAccessContext
    )
        : base(corpora)
    {
        _options = options;
        _corpusAggregator = corpusAggregator;
        _dataAccessContext = dataAccessContext;
    }

    public async Task<IEnumerable<Corpus>> GetAllAsync(string owner)
    {
        return await Entities.GetAllAsync(c => c.Owner == owner);
    }

    public override async Task CreateAsync(Corpus corpus)
    {
        await _dataAccessContext.BeginTransactionAsync();
        await base.CreateAsync(corpus);
        await _corpusAggregator.InsertCorpusAsync(corpus.Id, corpus.Owner, corpus.Type, corpus.Format);
        await _dataAccessContext.CommitTransactionAsync();
    }

    public override async Task<bool> DeleteAsync(string id)
    {
        await _dataAccessContext.BeginTransactionAsync();
        bool result = await base.DeleteAsync(id);
        await _corpusAggregator.DeleteCorpusAsync(id);
        await _dataAccessContext.CommitTransactionAsync();
        return result;
    }

    public async Task AddDataFileAsync(string id, DataFile dataFile, Stream stream)
    {
        dataFile.Filename = Path.GetRandomFileName();
        string path = GetDataFilePath(dataFile);
        using (FileStream fileStream = File.Create(path))
        {
            await stream.CopyToAsync(fileStream);
        }

        await _dataAccessContext.BeginTransactionAsync();
        await Entities.UpdateAsync(id, u => u.Add(c => c.Files, dataFile));
        await _corpusAggregator.AddDataFileAsync(
            id,
            dataFile.Id,
            dataFile.TextId ?? dataFile.Name,
            dataFile.LanguageTag,
            dataFile.Filename
        );
        await _dataAccessContext.CommitTransactionAsync();
    }

    public async Task<bool> DeleteDataFileAsync(string id, string fileId)
    {
        Corpus? corpus = await GetAsync(id);
        if (corpus == null)
            return false;
        DataFile? file = corpus.Files.FirstOrDefault(f => f.Id == fileId);
        if (file == null)
            return false;

        await _dataAccessContext.BeginTransactionAsync();
        await Entities.UpdateAsync(id, u => u.RemoveAll(c => c.Files, f => f.Id == fileId));
        await _corpusAggregator.DeleteDataFileAsync(id, fileId);
        await _dataAccessContext.CommitTransactionAsync();

        string path = GetDataFilePath(file);
        if (File.Exists(path))
            File.Delete(path);
        return true;
    }

    private string GetDataFilePath(DataFile dataFile)
    {
        return Path.Combine(_options.CurrentValue.DataFilesDir, dataFile.Filename);
    }
}
