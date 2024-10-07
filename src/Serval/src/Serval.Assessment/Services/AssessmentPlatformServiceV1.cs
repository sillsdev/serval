using Serval.Assessment.V1;
using Serval.Engine.V1;

namespace Serval.Assessment.Services;

public class AssessmentPlatformServiceV1(
    IRepository<AssessmentJob> jobs,
    IRepository<AssessmentEngine> engines,
    IRepository<AssessmentResult> results,
    IDataAccessContext dataAccessContext,
    IPublishEndpoint publishEndpoint
)
    : EnginePlatformServiceBaseV1<AssessmentJob, AssessmentEngine, AssessmentResult>(
        jobs,
        engines,
        results,
        dataAccessContext,
        publishEndpoint
    )
{
    protected override async Task<AssessmentEngine?> UpdateEngineAfterJobCompleted(
        AssessmentJob build,
        string engineId,
        JobCompletedRequest request,
        CancellationToken ct
    )
    {
        var parameters = JsonSerializer.Deserialize<AssessmentEngineCompletedStatistics>(request.StatisticsSerialized)!;
        return await Engines.UpdateAsync(
            engineId,
            u => u.Set(e => e.IsJobRunning, false).Inc(e => e.JobRevision),
            cancellationToken: ct
        );
    }

    protected override AssessmentResult CreateResultFromRequest(InsertResultsRequest request, int nextJobRevision)
    {
        var content = JsonSerializer.Deserialize<AssessmentResultContent>(request.ContentSerialized)!;
        return new AssessmentResult
        {
            EngineRef = request.EngineId,
            JobRevision = nextJobRevision,
            TextId = content.TextId,
            Ref = content.Ref,
            Score = content.Score,
            Description = content.Description,
        };
    }
}
