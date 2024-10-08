using Google.Protobuf.WellKnownTypes;
using Serval.Engine.V1;

namespace Serval.Shared.Services;

public abstract class EnginePlatformServiceBaseV1<TBuild, TEngine, TResults>(
    IRepository<TBuild> builds,
    IRepository<TEngine> engines,
    IRepository<TResults> results,
    IDataAccessContext dataAccessContext,
    IPublishEndpoint publishEndpoint
) : EnginePlatformApi.EnginePlatformApiBase
    where TBuild : IBuild
    where TEngine : IEngine
    where TResults : IBuildResult
{
    private const int ResultInsertBatchSize = 128;
    protected static readonly Empty Empty = new();

    protected readonly IRepository<TBuild> Builds = builds;
    protected readonly IRepository<TEngine> Engines = engines;
    protected readonly IRepository<TResults> Results = results;
    private readonly IDataAccessContext _dataAccessContext = dataAccessContext;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;

    public override async Task<Empty> BuildStarted(BuildStartedRequest request, ServerCallContext context)
    {
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                TBuild? build = await Builds.UpdateAsync(
                    request.BuildId,
                    u => u.Set(b => b.State, BuildState.Active),
                    cancellationToken: ct
                );
                if (build is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The build does not exist."));

                TEngine? engine = await Engines.UpdateAsync(
                    build.EngineRef,
                    u => u.Set(e => e.IsBuilding, true),
                    cancellationToken: ct
                );
                if (engine is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The engine does not exist."));

                await _publishEndpoint.Publish(
                    new BuildStarted
                    {
                        BuildId = build.Id,
                        EngineId = engine.Id,
                        Owner = engine.Owner,
                        Type = engine.Type
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
                TBuild? build = await Builds.UpdateAsync(
                    request.BuildId,
                    u =>
                        u.Set(b => b.State, BuildState.Completed)
                            .Set(b => b.Message, "Completed")
                            .Set(b => b.DateFinished, DateTime.UtcNow),
                    cancellationToken: ct
                );
                if (build is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The build does not exist."));

                TEngine? engine = await UpdateEngineAfterBuildCompleted(build, build.EngineRef, request, ct);

                if (engine is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The engine does not exist."));

                // delete pretranslations created by the previous build
                await Results.DeleteAllAsync(
                    p => p.EngineRef == engine.Id && p.BuildRevision < engine.BuildRevision,
                    ct
                );

                await _publishEndpoint.Publish(
                    new BuildFinished
                    {
                        BuildId = build.Id,
                        EngineId = engine.Id,
                        Owner = engine.Owner,
                        Type = engine.Type,
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

    protected virtual async Task<TEngine?> UpdateEngineAfterBuildCompleted(
        TBuild build,
        string engineId,
        BuildCompletedRequest request,
        CancellationToken ct
    )
    {
        return await Engines.UpdateAsync(
            engineId,
            u => u.Set(e => e.IsBuilding, false).Inc(e => e.BuildRevision),
            cancellationToken: ct
        );
    }

    public override async Task<Empty> BuildCanceled(BuildCanceledRequest request, ServerCallContext context)
    {
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                TBuild? build = await Builds.UpdateAsync(
                    request.BuildId,
                    u =>
                        u.Set(b => b.Message, "Canceled")
                            .Set(b => b.DateFinished, DateTime.UtcNow)
                            .Set(b => b.State, BuildState.Canceled),
                    cancellationToken: ct
                );
                if (build is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The build does not exist."));

                TEngine? engine = await Engines.UpdateAsync(
                    build.EngineRef,
                    u => u.Set(e => e.IsBuilding, false),
                    cancellationToken: ct
                );
                if (engine is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The engine does not exist."));

                // delete pretranslations that might have been created during the build
                await Results.DeleteAllAsync(
                    p => p.EngineRef == engine.Id && p.BuildRevision > engine.BuildRevision,
                    ct
                );

                await _publishEndpoint.Publish(
                    new BuildFinished
                    {
                        BuildId = build.Id,
                        EngineId = engine.Id,
                        Owner = engine.Owner,
                        Type = engine.Type,
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
                TBuild? build = await Builds.UpdateAsync(
                    request.BuildId,
                    u =>
                        u.Set(b => b.State, BuildState.Faulted)
                            .Set(b => b.Message, request.Message)
                            .Set(b => b.DateFinished, DateTime.UtcNow),
                    cancellationToken: ct
                );
                if (build is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The build does not exist."));

                TEngine? engine = await Engines.UpdateAsync(
                    build.EngineRef,
                    u => u.Set(e => e.IsBuilding, false),
                    cancellationToken: ct
                );
                if (engine is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The engine does not exist."));

                // delete pretranslations that might have been created during the build
                await Results.DeleteAllAsync(
                    p => p.EngineRef == engine.Id && p.BuildRevision > engine.BuildRevision,
                    ct
                );

                await _publishEndpoint.Publish(
                    new BuildFinished
                    {
                        BuildId = build.Id,
                        EngineId = engine.Id,
                        Owner = engine.Owner,
                        Type = engine.Type,
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
                TBuild? build = await UpdateBuildUponRestarting(request, ct);
                if (build is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The build does not exist."));

                TEngine? engine = await Engines.GetAsync(build.EngineRef, ct);
                if (engine is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The engine does not exist."));

                // delete pretranslations that might have been created during the build
                await Results.DeleteAllAsync(
                    p => p.EngineRef == engine.Id && p.BuildRevision > engine.BuildRevision,
                    ct
                );
            },
            cancellationToken: context.CancellationToken
        );

        return Empty;
    }

    protected virtual async Task<TBuild?> UpdateBuildUponRestarting(
        BuildRestartingRequest request,
        CancellationToken ct
    )
    {
        TBuild? build = await Builds.UpdateAsync(
            request.BuildId,
            u =>
                u.Set(b => b.Message, "Restarting")
                    .Set(b => b.PercentCompleted, 0)
                    .Set(b => b.State, BuildState.Pending),
            cancellationToken: ct
        );
        return build;
    }

    public override async Task<Empty> UpdateBuildStatus(UpdateBuildStatusRequest request, ServerCallContext context)
    {
        await Builds.UpdateAsync(
            b => b.Id == request.BuildId && (b.State == BuildState.Active || b.State == BuildState.Pending),
            u =>
            {
                if (request.HasPercentCompleted)
                {
                    u.Set(
                        b => b.PercentCompleted,
                        Math.Round(request.PercentCompleted, 4, MidpointRounding.AwayFromZero)
                    );
                }
                if (request.HasMessage)
                    u.Set(b => b.Message, request.Message);
                if (request.HasQueueDepth)
                    u.Set(b => b.QueueDepth, request.QueueDepth);
            },
            cancellationToken: context.CancellationToken
        );

        return Empty;
    }

    public override async Task<Empty> InsertResults(
        IAsyncStreamReader<InsertResultsRequest> requestStream,
        ServerCallContext context
    )
    {
        string engineId = "";
        int nextBuildRevision = 0;

        var batch = new List<TResults>();
        await foreach (InsertResultsRequest request in requestStream.ReadAllAsync(context.CancellationToken))
        {
            if (request.EngineId != engineId)
            {
                TEngine? engine = await Engines.GetAsync(request.EngineId, context.CancellationToken);
                if (engine is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The engine does not exist."));
                nextBuildRevision = engine.BuildRevision + 1;
                engineId = request.EngineId;
            }
            batch.Add(CreateResultFromRequest(request, nextBuildRevision));
            if (batch.Count == ResultInsertBatchSize)
            {
                await Results.InsertAllAsync(batch, context.CancellationToken);
                batch.Clear();
            }
        }
        if (batch.Count > 0)
            await Results.InsertAllAsync(batch, CancellationToken.None);

        return Empty;
    }

    protected abstract TResults CreateResultFromRequest(InsertResultsRequest request, int nextBuildRevision);
}
