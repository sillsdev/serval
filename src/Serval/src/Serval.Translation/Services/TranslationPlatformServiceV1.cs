using Google.Protobuf.WellKnownTypes;
using Serval.Translation.V1;

namespace Serval.Translation.Services;

public class TranslationPlatformServiceV1(
    IRepository<Build> builds,
    IRepository<Engine> engines,
    IRepository<Pretranslation> pretranslations,
    IDataAccessContext dataAccessContext,
    IPublishEndpoint publishEndpoint
) : TranslationPlatformApi.TranslationPlatformApiBase
{
    private const int PretranslationInsertBatchSize = 128;
    private static readonly Empty Empty = new();

    private readonly IRepository<Build> _builds = builds;
    private readonly IRepository<Engine> _engines = engines;
    private readonly IRepository<Pretranslation> _pretranslations = pretranslations;
    private readonly IDataAccessContext _dataAccessContext = dataAccessContext;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;

    public override async Task<Empty> BuildStarted(BuildStartedRequest request, ServerCallContext context)
    {
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                Build? build = await _builds.UpdateAsync(
                    request.BuildId,
                    u => u.Set(b => b.State, JobState.Active),
                    cancellationToken: ct
                );
                if (build is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The build does not exist."));

                Engine? engine = await _engines.UpdateAsync(
                    build.EngineRef,
                    u => u.Set(e => e.IsBuilding, true),
                    cancellationToken: ct
                );
                if (engine is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The engine does not exist."));

                await _publishEndpoint.Publish(
                    new TranslationBuildStarted
                    {
                        BuildId = build.Id,
                        EngineId = engine.Id,
                        Owner = engine.Owner
                    },
                    ct
                );
            },
            cancellationToken: context.CancellationToken
        );
        return Empty;
    }

    public override async Task<Empty> BuildCompleted(BuildCompletedRequest request, ServerCallContext context)
    {
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                Build? build = await _builds.UpdateAsync(
                    request.BuildId,
                    u =>
                        u.Set(b => b.State, JobState.Completed)
                            .Set(b => b.Message, "Completed")
                            .Set(b => b.DateFinished, DateTime.UtcNow),
                    cancellationToken: ct
                );
                if (build is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The build does not exist."));

                Engine? engine = await _engines.UpdateAsync(
                    build.EngineRef,
                    u =>
                        u.Set(e => e.Confidence, request.Confidence)
                            .Set(e => e.CorpusSize, request.CorpusSize)
                            .Set(e => e.IsBuilding, false)
                            .Inc(e => e.ModelRevision),
                    cancellationToken: ct
                );
                if (engine is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The engine does not exist."));

                // delete pretranslations created by the previous build
                await _pretranslations.DeleteAllAsync(
                    p => p.EngineRef == engine.Id && p.ModelRevision < engine.ModelRevision,
                    ct
                );

                await _publishEndpoint.Publish(
                    new TranslationBuildFinished
                    {
                        BuildId = build.Id,
                        EngineId = engine.Id,
                        Owner = engine.Owner,
                        BuildState = build.State,
                        Message = build.Message!,
                        DateFinished = build.DateFinished!.Value,
                    },
                    ct
                );
            },
            cancellationToken: context.CancellationToken
        );

        return Empty;
    }

    public override async Task<Empty> BuildCanceled(BuildCanceledRequest request, ServerCallContext context)
    {
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                Build? build = await _builds.UpdateAsync(
                    request.BuildId,
                    u =>
                        u.Set(b => b.Message, "Canceled")
                            .Set(b => b.DateFinished, DateTime.UtcNow)
                            .Set(b => b.State, JobState.Canceled),
                    cancellationToken: ct
                );
                if (build is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The build does not exist."));

                Engine? engine = await _engines.UpdateAsync(
                    build.EngineRef,
                    u => u.Set(e => e.IsBuilding, false),
                    cancellationToken: ct
                );
                if (engine is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The engine does not exist."));

                // delete pretranslations that might have been created during the build
                await _pretranslations.DeleteAllAsync(
                    p => p.EngineRef == engine.Id && p.ModelRevision > engine.ModelRevision,
                    ct
                );

                await _publishEndpoint.Publish(
                    new TranslationBuildFinished
                    {
                        BuildId = build.Id,
                        EngineId = engine.Id,
                        Owner = engine.Owner,
                        BuildState = build.State,
                        Message = build.Message!,
                        DateFinished = build.DateFinished!.Value
                    },
                    ct
                );
            },
            cancellationToken: context.CancellationToken
        );

        return Empty;
    }

    public override async Task<Empty> BuildFaulted(BuildFaultedRequest request, ServerCallContext context)
    {
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                Build? build = await _builds.UpdateAsync(
                    request.BuildId,
                    u =>
                        u.Set(b => b.State, JobState.Faulted)
                            .Set(b => b.Message, request.Message)
                            .Set(b => b.DateFinished, DateTime.UtcNow),
                    cancellationToken: ct
                );
                if (build is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The build does not exist."));

                Engine? engine = await _engines.UpdateAsync(
                    build.EngineRef,
                    u => u.Set(e => e.IsBuilding, false),
                    cancellationToken: ct
                );
                if (engine is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The engine does not exist."));

                // delete pretranslations that might have been created during the build
                await _pretranslations.DeleteAllAsync(
                    p => p.EngineRef == engine.Id && p.ModelRevision > engine.ModelRevision,
                    ct
                );

                await _publishEndpoint.Publish(
                    new TranslationBuildFinished
                    {
                        BuildId = build.Id,
                        EngineId = engine.Id,
                        Owner = engine.Owner,
                        BuildState = build.State,
                        Message = build.Message!,
                        DateFinished = build.DateFinished!.Value
                    },
                    ct
                );
            },
            cancellationToken: context.CancellationToken
        );

        return Empty;
    }

    public override async Task<Empty> BuildRestarting(BuildRestartingRequest request, ServerCallContext context)
    {
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                Build? build = await _builds.UpdateAsync(
                    request.BuildId,
                    u =>
                        u.Set(b => b.Message, "Restarting")
                            .Set(b => b.Step, 0)
                            .Set(b => b.Progress, 0)
                            .Set(b => b.State, JobState.Pending),
                    cancellationToken: ct
                );
                if (build is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The build does not exist."));

                Engine? engine = await _engines.GetAsync(build.EngineRef, ct);
                if (engine is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The engine does not exist."));

                // delete pretranslations that might have been created during the build
                await _pretranslations.DeleteAllAsync(
                    p => p.EngineRef == engine.Id && p.ModelRevision > engine.ModelRevision,
                    ct
                );
            },
            cancellationToken: context.CancellationToken
        );

        return Empty;
    }

    public override async Task<Empty> UpdateBuildStatus(UpdateBuildStatusRequest request, ServerCallContext context)
    {
        await _builds.UpdateAsync(
            b => b.Id == request.BuildId && (b.State == JobState.Active || b.State == JobState.Pending),
            u =>
            {
                u.Set(b => b.Step, request.Step);
                if (request.HasProgress)
                    u.Set(b => b.Progress, Math.Round(request.Progress, 4, MidpointRounding.AwayFromZero));
                if (request.HasMessage)
                    u.Set(b => b.Message, request.Message);
                if (request.HasQueueDepth)
                    u.Set(b => b.QueueDepth, request.QueueDepth);
                if (request.Phases.Count > 0)
                {
                    u.Set(
                        b => b.Phases,
                        request
                            .Phases.Select(p => new BuildPhase
                            {
                                Stage = (BuildPhaseStage)p.Stage,
                                Step = p.HasStep ? p.Step : null,
                                StepCount = p.HasStepCount ? p.StepCount : null
                            })
                            .ToList()
                    );
                }
            },
            cancellationToken: context.CancellationToken
        );

        return Empty;
    }

    public override async Task<Empty> UpdateBuildExecutionData(
        UpdateBuildExecutionDataRequest request,
        ServerCallContext context
    )
    {
        await _builds.UpdateAsync(
            b => b.Id == request.BuildId,
            u =>
            {
                // initialize ExecutionData if it's null
                foreach (KeyValuePair<string, string> entry in request.ExecutionData)
                    u.Set(b => b.ExecutionData[entry.Key], entry.Value);
            },
            cancellationToken: context.CancellationToken
        );

        return new Empty();
    }

    public override async Task<Empty> UpdateParallelCorpusAnalysis(
        UpdateParallelCorpusAnalysisRequest request,
        ServerCallContext context
    )
    {
        await _builds.UpdateAsync(
            b => b.Id == request.BuildId && b.EngineRef == request.EngineId,
            u =>
            {
                if (request.ParallelCorpusAnalysis.Count > 0)
                {
                    u.Set(
                        b => b.Analysis,
                        request
                            .ParallelCorpusAnalysis.Select(a => new ParallelCorpusAnalysis
                            {
                                ParallelCorpusRef = a.ParallelCorpusId,
                                SourceQuoteConvention = a.SourceQuoteConvention,
                                TargetQuoteConvention = a.TargetQuoteConvention,
                            })
                            .ToList()
                    );
                }
            },
            cancellationToken: context.CancellationToken
        );

        return Empty;
    }

    public override async Task<Empty> IncrementEngineCorpusSize(
        IncrementEngineCorpusSizeRequest request,
        ServerCallContext context
    )
    {
        await _engines.UpdateAsync(
            request.EngineId,
            u => u.Inc(e => e.CorpusSize, request.Count),
            cancellationToken: context.CancellationToken
        );
        return Empty;
    }

    public override async Task<Empty> InsertPretranslations(
        IAsyncStreamReader<InsertPretranslationsRequest> requestStream,
        ServerCallContext context
    )
    {
        string engineId = "";
        int nextModelRevision = 0;

        var batch = new List<Pretranslation>();
        await foreach (InsertPretranslationsRequest request in requestStream.ReadAllAsync(context.CancellationToken))
        {
            if (request.EngineId != engineId)
            {
                Engine? engine = await _engines.GetAsync(request.EngineId, context.CancellationToken);
                if (engine is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The engine does not exist."));
                nextModelRevision = engine.ModelRevision + 1;
                engineId = request.EngineId;
            }
            batch.Add(
                new Pretranslation
                {
                    EngineRef = request.EngineId,
                    ModelRevision = nextModelRevision,
                    CorpusRef = request.CorpusId,
                    TextId = request.TextId,
                    Refs = request.Refs.ToList(),
                    Translation = request.Translation,
                    SourceTokens = request.SourceTokens,
                    TranslationTokens = request.TranslationTokens,
                    Alignment = request.Alignment.Select(Map).ToList()
                }
            );
            if (batch.Count == PretranslationInsertBatchSize)
            {
                await _pretranslations.InsertAllAsync(batch, context.CancellationToken);
                batch.Clear();
            }
        }
        if (batch.Count > 0)
            await _pretranslations.InsertAllAsync(batch, CancellationToken.None);

        return Empty;
    }

    private Models.AlignedWordPair Map(V1.AlignedWordPair alignedWordPair)
    {
        return new Models.AlignedWordPair()
        {
            SourceIndex = alignedWordPair.SourceIndex,
            TargetIndex = alignedWordPair.TargetIndex
        };
    }
}
