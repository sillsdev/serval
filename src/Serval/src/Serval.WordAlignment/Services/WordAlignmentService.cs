namespace Serval.WordAlignment.Services;

public class WordAlignmentService(IRepository<Models.WordAlignment> wordAlignments)
    : EntityServiceBase<Models.WordAlignment>(wordAlignments),
        IWordAlignmentService
{
    public async Task<IEnumerable<Models.WordAlignment>> GetAllAsync(
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
