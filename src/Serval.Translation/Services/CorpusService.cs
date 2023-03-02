namespace Serval.Translation.Services;

public class CorpusService : EntityServiceBase<Corpus>, ICorpusService
{
    public CorpusService(IRepository<Corpus> corpora)
        : base(corpora) { }
}
