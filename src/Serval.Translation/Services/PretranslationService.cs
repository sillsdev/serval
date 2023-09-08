namespace Serval.Translation.Services;

public class PretranslationService : EntityServiceBase<Pretranslation>, IPretranslationService
{
    public PretranslationService(IRepository<Pretranslation> pretranslations)
        : base(pretranslations) { }

    public async Task<IEnumerable<Pretranslation>> GetAllAsync(
        string engineId,
        int modelRevision,
        string corpusId,
        string? textId = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Entities.GetAllAsync(
            pt =>
                pt.EngineRef == engineId
                && pt.ModelRevision == modelRevision
                && pt.CorpusRef == corpusId
                && (textId == null || pt.TextId == textId),
            cancellationToken
        );
    }
}
