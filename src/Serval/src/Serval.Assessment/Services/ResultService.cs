namespace Serval.Assessment.Services;

public class ResultService(IRepository<AssessmentResult> results)
    : EntityServiceBase<AssessmentResult>(results),
        IResultService
{
    public async Task<IEnumerable<AssessmentResult>> GetAllAsync(
        string engineId,
        string jobId,
        string? textId = null,
        CancellationToken cancellationToken = default
    )
    {
        return await Entities.GetAllAsync(
            r => r.EngineRef == engineId && r.JobRef == jobId && (textId == null || r.TextId == textId),
            cancellationToken
        );
    }
}
