using Serval.Engine.V1;
using Serval.Translation.V1;

namespace Serval.Translation.Services;

public class TranslationPlatformServiceV1(
    IRepository<TranslationBuild> builds,
    IRepository<TranslationEngine> engines,
    IRepository<Pretranslation> pretranslations,
    IDataAccessContext dataAccessContext,
    IPublishEndpoint publishEndpoint
)
    : EnginePlatformServiceBaseV1<TranslationBuild, TranslationEngine, Pretranslation>(
        builds,
        engines,
        pretranslations,
        dataAccessContext,
        publishEndpoint
    )
{
    protected override async Task<TranslationEngine?> UpdateEngineAfterBuildCompleted(
        TranslationBuild build,
        string engineId,
        BuildCompletedRequest request,
        CancellationToken ct
    )
    {
        var parameters = JsonSerializer.Deserialize<TranslationEngineCompletedStatistics>(
            request.StatisticsSerialized
        )!;
        return await Engines.UpdateAsync(
            engineId,
            u =>
                u.Set(e => e.IsBuilding, false)
                    .Set(e => e.Confidence, parameters.Confidence)
                    .Set(e => e.CorpusSize, parameters.CorpusSize)
                    .Inc(e => e.BuildRevision),
            cancellationToken: ct
        );
    }

    protected override Pretranslation CreateResultFromRequest(InsertResultsRequest request, int nextBuildRevision)
    {
        var content = JsonSerializer.Deserialize<TranslationResultContent>(request.ContentSerialized)!;
        return new Pretranslation
        {
            EngineRef = request.EngineId,
            BuildRevision = nextBuildRevision,
            CorpusRef = content.CorpusId,
            TextId = content.TextId,
            Refs = content.Refs.ToList(),
            Translation = content.Translation,
        };
    }
}
