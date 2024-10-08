using Serval.Assessment.V1;
using Serval.Engine.V1;

namespace Serval.Assessment.Services;

public class AssessmentPlatformServiceV1(
    IRepository<AssessmentBuild> builds,
    IRepository<AssessmentEngine> engines,
    IRepository<AssessmentResult> results,
    IDataAccessContext dataAccessContext,
    IPublishEndpoint publishEndpoint
)
    : EnginePlatformServiceBaseV1<AssessmentBuild, AssessmentEngine, AssessmentResult>(
        builds,
        engines,
        results,
        dataAccessContext,
        publishEndpoint
    )
{
    protected override async Task<AssessmentEngine?> UpdateEngineAfterBuildCompleted(
        AssessmentBuild build,
        string engineId,
        BuildCompletedRequest request,
        CancellationToken ct
    )
    {
        var parameters = JsonSerializer.Deserialize<AssessmentEngineCompletedStatistics>(request.StatisticsSerialized)!;
        return await Engines.UpdateAsync(
            engineId,
            u => u.Set(e => e.IsBuilding, false).Inc(e => e.BuildRevision),
            cancellationToken: ct
        );
    }

    protected override AssessmentResult CreateResultFromRequest(InsertResultsRequest request, int nextBuildRevision)
    {
        var content = JsonSerializer.Deserialize<AssessmentResultContent>(request.ContentSerialized)!;
        return new AssessmentResult
        {
            EngineRef = request.EngineId,
            BuildRevision = nextBuildRevision,
            TextId = content.TextId,
            Ref = content.Ref,
            Score = content.Score,
            Description = content.Description,
        };
    }
}
