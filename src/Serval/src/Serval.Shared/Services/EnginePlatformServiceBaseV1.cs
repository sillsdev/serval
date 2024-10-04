using Google.Protobuf.WellKnownTypes;
using Serval.Engine.V1;

namespace Serval.Shared.Services;

public abstract class EnginePlatformServiceBaseV1<TJob, TEngine, TResults>(
    IRepository<TJob> jobs,
    IRepository<TEngine> engines,
    IRepository<TResults> results,
    IDataAccessContext dataAccessContext,
    IPublishEndpoint publishEndpoint
) : EnginePlatformApi.EnginePlatformApiBase
    where TJob : IJob
    where TEngine : IEngine
    where TResults : Models.IResult
{
    private const int ResultInsertBatchSize = 128;
    protected static readonly Empty Empty = new();

    protected readonly IRepository<TJob> Jobs = jobs;
    protected readonly IRepository<TEngine> Engines = engines;
    protected readonly IRepository<TResults> Results = results;
    private readonly IDataAccessContext _dataAccessContext = dataAccessContext;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;

    public override async Task<Empty> JobStarted(JobStartedRequest request, ServerCallContext context)
    {
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                TJob? build = await Jobs.UpdateAsync(
                    request.JobId,
                    u => u.Set(b => b.State, JobState.Active),
                    cancellationToken: ct
                );
                if (build is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The build does not exist."));

                TEngine? engine = await Engines.UpdateAsync(
                    build.EngineRef,
                    u => u.Set(e => e.IsJobRunning, true),
                    cancellationToken: ct
                );
                if (engine is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The engine does not exist."));

                await _publishEndpoint.Publish(
                    new JobStarted
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

    public override async Task<Empty> JobCompleted(JobCompletedRequest request, ServerCallContext context)
    {
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                TJob? build = await Jobs.UpdateAsync(
                    request.JobId,
                    u =>
                        u.Set(b => b.State, JobState.Completed)
                            .Set(b => b.Message, "Completed")
                            .Set(b => b.DateFinished, DateTime.UtcNow),
                    cancellationToken: ct
                );
                if (build is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The build does not exist."));

                TEngine? engine = await UpdateEngineAfterJobCompleted(build, build.EngineRef, request, ct);

                if (engine is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The engine does not exist."));

                // delete pretranslations created by the previous build
                await Results.DeleteAllAsync(p => p.EngineRef == engine.Id && p.JobRevision < engine.JobRevision, ct);

                await _publishEndpoint.Publish(
                    new JobFinished
                    {
                        JobId = build.Id,
                        EngineId = engine.Id,
                        Owner = engine.Owner,
                        Type = engine.Type,
                        JobState = build.State,
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

    protected virtual async Task<TEngine?> UpdateEngineAfterJobCompleted(
        TJob build,
        string engineId,
        JobCompletedRequest request,
        CancellationToken ct
    )
    {
        return await Engines.UpdateAsync(
            engineId,
            u => u.Set(e => e.IsJobRunning, false).Inc(e => e.JobRevision),
            cancellationToken: ct
        );
    }

    public override async Task<Empty> JobCanceled(JobCanceledRequest request, ServerCallContext context)
    {
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                TJob? build = await Jobs.UpdateAsync(
                    request.JobId,
                    u =>
                        u.Set(b => b.Message, "Canceled")
                            .Set(b => b.DateFinished, DateTime.UtcNow)
                            .Set(b => b.State, JobState.Canceled),
                    cancellationToken: ct
                );
                if (build is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The build does not exist."));

                TEngine? engine = await Engines.UpdateAsync(
                    build.EngineRef,
                    u => u.Set(e => e.IsJobRunning, false),
                    cancellationToken: ct
                );
                if (engine is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The engine does not exist."));

                // delete pretranslations that might have been created during the build
                await Results.DeleteAllAsync(p => p.EngineRef == engine.Id && p.JobRevision > engine.JobRevision, ct);

                await _publishEndpoint.Publish(
                    new JobFinished
                    {
                        JobId = build.Id,
                        EngineId = engine.Id,
                        Owner = engine.Owner,
                        Type = engine.Type,
                        JobState = build.State,
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

    public override async Task<Empty> JobFaulted(JobFaultedRequest request, ServerCallContext context)
    {
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                TJob? build = await Jobs.UpdateAsync(
                    request.JobId,
                    u =>
                        u.Set(b => b.State, JobState.Faulted)
                            .Set(b => b.Message, request.Message)
                            .Set(b => b.DateFinished, DateTime.UtcNow),
                    cancellationToken: ct
                );
                if (build is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The build does not exist."));

                TEngine? engine = await Engines.UpdateAsync(
                    build.EngineRef,
                    u => u.Set(e => e.IsJobRunning, false),
                    cancellationToken: ct
                );
                if (engine is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The engine does not exist."));

                // delete pretranslations that might have been created during the build
                await Results.DeleteAllAsync(p => p.EngineRef == engine.Id && p.JobRevision > engine.JobRevision, ct);

                await _publishEndpoint.Publish(
                    new JobFinished
                    {
                        JobId = build.Id,
                        EngineId = engine.Id,
                        Owner = engine.Owner,
                        Type = engine.Type,
                        JobState = build.State,
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

    public override async Task<Empty> JobRestarting(JobRestartingRequest request, ServerCallContext context)
    {
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                TJob? build = await UpdateJobUponRestarting(request, ct);
                if (build is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The build does not exist."));

                TEngine? engine = await Engines.GetAsync(build.EngineRef, ct);
                if (engine is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The engine does not exist."));

                // delete pretranslations that might have been created during the build
                await Results.DeleteAllAsync(p => p.EngineRef == engine.Id && p.JobRevision > engine.JobRevision, ct);
            },
            cancellationToken: context.CancellationToken
        );

        return Empty;
    }

    protected virtual async Task<TJob?> UpdateJobUponRestarting(JobRestartingRequest request, CancellationToken ct)
    {
        TJob? job = await Jobs.UpdateAsync(
            request.JobId,
            u =>
                u.Set(b => b.Message, "Restarting").Set(b => b.PercentCompleted, 0).Set(b => b.State, JobState.Pending),
            cancellationToken: ct
        );
        return job;
    }

    public override async Task<Empty> UpdateJobStatus(UpdateJobStatusRequest request, ServerCallContext context)
    {
        await Jobs.UpdateAsync(
            b => b.Id == request.JobId && (b.State == JobState.Active || b.State == JobState.Pending),
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
        int nextJobRevision = 0;

        var batch = new List<TResults>();
        await foreach (InsertResultsRequest request in requestStream.ReadAllAsync(context.CancellationToken))
        {
            if (request.EngineId != engineId)
            {
                TEngine? engine = await Engines.GetAsync(request.EngineId, context.CancellationToken);
                if (engine is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The engine does not exist."));
                nextJobRevision = engine.JobRevision + 1;
                engineId = request.EngineId;
            }
            batch.Add(CreateResultFromRequest(request, nextJobRevision));
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

    protected abstract TResults CreateResultFromRequest(InsertResultsRequest request, int nextJobRevision);
}
