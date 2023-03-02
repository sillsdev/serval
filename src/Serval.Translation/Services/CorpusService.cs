namespace Serval.Translation.Services;

public class CorpusService : EntityServiceBase<Corpus>, ICorpusService
{
    private readonly IIdGenerator _idGenerator;

    public CorpusService(IRepository<Corpus> corpora, IIdGenerator idGenerator)
        : base(corpora)
    {
        _idGenerator = idGenerator;
    }

    public async Task<IEnumerable<Corpus>> GetAllAsync(string owner)
    {
        return await Entities.GetAllAsync(c => c.Owner == owner);
    }

    public async Task AddDataFileAsync(string id, CorpusFile dataFile)
    {
        dataFile.Id = _idGenerator.GenerateId();
        await Entities.UpdateAsync(id, u => u.Add(c => c.Files, dataFile));
    }

    public async Task<bool> DeleteDataFileAsync(string id, string fileId)
    {
        Corpus? corpus = await Entities.UpdateAsync(id, u => u.RemoveAll(c => c.Files, f => f.Id == fileId));
        return corpus is not null;
    }
}
