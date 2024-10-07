using Serval.Assessment.V1;
using Serval.Engine.V1;

namespace Serval.Assessment.Services;

public class AssessmentPlatformServiceV1(
    IRepository<AssessmentBuild> jobs,
    IRepository<AssessmentEngine> engines,
    IRepository<AssessmentResult> results,
    IDataAccessContext dataAccessContext,
    IPublishEndpoint publishEndpoint
)
    : EnginePlatformServiceBaseV1<AssessmentBuild, AssessmentEngine, AssessmentResult>(
        jobs,
        engines,
        results,
        dataAccessContext,
        publishEndpoint
    )
{
    protected override async Task<AssessmentEngine?> UpdateEngineAfterJobCompleted(
        AssessmentBuild build,
        string engineId,
        JobCompletedRequest request,
        CancellationToken ct
    )
    {
        var parameters = JsonSerializer.Deserialize<AssessmentEngineCompletedStatistics>(request.StatisticsSerialized)!;
        return await Engines.UpdateAsync(
            engineId,
            u => u.Set(e => e.IsBuildRunning, false).Inc(e => e.BuildRevision),
            cancellationToken: ct
        );
    }

    protected override AssessmentResult CreateResultFromRequest(InsertResultsRequest request, int nextJobRevision)
    {
        var content = JsonSerializer.Deserialize<AssessmentResultContent>(request.ContentSerialized)!;
        return new AssessmentResult
        {
            EngineRef = request.EngineId,
            BuildRevision = nextJobRevision,
            TextId = content.TextId,
            Ref = content.Ref,
            Score = content.Score,
            Description = content.Description,
        };
    }
}
