using Google.Protobuf.WellKnownTypes;
using Serval.Engine.V1;
using Serval.Translation.V1;

namespace Serval.Translation.Services;

public class TranslationPlatformServiceV1(
    IRepository<TranslationBuildJob> jobs,
    IRepository<TranslationEngine> engines,
    IRepository<Pretranslation> pretranslations,
    IDataAccessContext dataAccessContext,
    IPublishEndpoint publishEndpoint
)
    : EnginePlatformServiceBaseV1<TranslationBuildJob, TranslationEngine, Pretranslation>(
        jobs,
        engines,
        pretranslations,
        dataAccessContext,
        publishEndpoint
    )
{
    protected override async Task<TranslationEngine?> UpdateEngineAfterJobCompleted(
        TranslationBuildJob build,
        string engineId,
        JobCompletedRequest request,
        CancellationToken ct
    )
    {
        var parameters = JsonSerializer.Deserialize<TranslationEngineCompletedStatistics>(
            request.StatisticsSerialized
        )!;
        return await Engines.UpdateAsync(
            engineId,
            u =>
                u.Set(e => e.IsJobRunning, false)
                    .Set(e => e.Confidence, parameters.Confidence)
                    .Set(e => e.CorpusSize, parameters.CorpusSize)
                    .Inc(e => e.JobRevision),
            cancellationToken: ct
        );
    }

    protected override Pretranslation CreateResultFromRequest(InsertResultsRequest request, int nextJobRevision)
    {
        var content = JsonSerializer.Deserialize<TranslationResultContent>(request.ContentSerialized)!;
        return new Pretranslation
        {
            EngineRef = request.EngineId,
            JobRevision = nextJobRevision,
            CorpusRef = content.CorpusId,
            TextId = content.TextId,
            Refs = content.Refs.ToList(),
            Translation = content.Pretranslation,
        };
    }
}
