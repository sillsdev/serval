using Google.Protobuf.WellKnownTypes;
using Serval.Assessment.V1;

namespace Serval.Assessment.Services;

public class AssessmentPlatformServiceV1(
    IRepository<AssessmentJob> jobs,
    IRepository<AssessmentEngine> engines,
    IRepository<Result> results,
    IDataAccessContext dataAccessContext,
    IPublishEndpoint publishEndpoint
) : AssessmentPlatformApi.AssessmentPlatformApiBase
{
    private const int ResultInsertBatchSize = 128;
    private static readonly Empty Empty = new();

    private readonly IRepository<AssessmentJob> _jobs = jobs;
    private readonly IRepository<AssessmentEngine> _engines = engines;
    private readonly IRepository<Result> _results = results;
    private readonly IDataAccessContext _dataAccessContext = dataAccessContext;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;

    public override async Task<Empty> JobStarted(JobStartedRequest request, ServerCallContext context)
    {
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                AssessmentJob? job = await _jobs.UpdateAsync(
                    request.JobId,
                    u => u.Set(b => b.State, JobState.Active),
                    cancellationToken: ct
                );
                if (job is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The job does not exist."));

                AssessmentEngine? engine = await _engines.GetAsync(job.EngineRef, cancellationToken: ct);
                if (engine is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The engine does not exist."));

                await _publishEndpoint.Publish(
                    new AssessmentJobStarted
                    {
                        JobId = job.Id,
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

    public override async Task<Empty> JobCompleted(JobCompletedRequest request, ServerCallContext context)
    {
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                AssessmentJob? job = await _jobs.UpdateAsync(
                    request.JobId,
                    u =>
                        u.Set(b => b.State, JobState.Completed)
                            .Set(b => b.Message, "Completed")
                            .Set(b => b.DateFinished, DateTime.UtcNow),
                    cancellationToken: ct
                );
                if (job is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The job does not exist."));

                AssessmentEngine? engine = await _engines.GetAsync(job.EngineRef, cancellationToken: ct);
                if (engine is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The engine does not exist."));

                await _publishEndpoint.Publish(
                    new AssessmentJobFinished
                    {
                        JobId = job.Id,
                        EngineId = engine.Id,
                        Owner = engine.Owner,
                        JobState = job.State,
                        Message = job.Message!,
                        DateFinished = job.DateFinished!.Value
                    },
                    ct
                );
            },
            cancellationToken: context.CancellationToken
        );

        return Empty;
    }

    public override async Task<Empty> JobCanceled(JobCanceledRequest request, ServerCallContext context)
    {
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                AssessmentJob? job = await _jobs.UpdateAsync(
                    request.JobId,
                    u =>
                    {
                        u.Set(j => j.Message, "Canceled");
                        u.Set(j => j.DateFinished, DateTime.UtcNow);
                        u.Set(j => j.State, JobState.Canceled);
                    },
                    cancellationToken: ct
                );
                if (job is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The job does not exist."));

                AssessmentEngine? engine = await _engines.GetAsync(job.EngineRef, cancellationToken: ct);
                if (engine is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The engine does not exist."));

                await _publishEndpoint.Publish(
                    new AssessmentJobFinished
                    {
                        JobId = job.Id,
                        EngineId = engine.Id,
                        Owner = engine.Owner,
                        JobState = job.State,
                        Message = job.Message!,
                        DateFinished = job.DateFinished!.Value
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
                AssessmentJob? job = await _jobs.UpdateAsync(
                    request.JobId,
                    u =>
                    {
                        u.Set(b => b.State, JobState.Faulted);
                        u.Set(b => b.Message, request.Message);
                        u.Set(b => b.DateFinished, DateTime.UtcNow);
                    },
                    cancellationToken: ct
                );
                if (job is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The job does not exist."));

                AssessmentEngine? engine = await _engines.GetAsync(job.EngineRef, cancellationToken: ct);
                if (engine is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The engine does not exist."));

                await _publishEndpoint.Publish(
                    new AssessmentJobFinished
                    {
                        JobId = job.Id,
                        EngineId = engine.Id,
                        Owner = engine.Owner,
                        JobState = job.State,
                        Message = job.Message!,
                        DateFinished = job.DateFinished!.Value
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
        AssessmentJob? job = await _jobs.UpdateAsync(
            request.JobId,
            u =>
            {
                u.Set(j => j.Message, "Restarting");
                u.Unset(j => j.PercentCompleted);
                u.Set(j => j.State, JobState.Pending);
            },
            cancellationToken: context.CancellationToken
        );
        if (job is null)
            throw new RpcException(new Status(StatusCode.NotFound, "The job does not exist."));

        return Empty;
    }

    public override async Task<Empty> UpdateJobStatus(UpdateJobStatusRequest request, ServerCallContext context)
    {
        await _jobs.UpdateAsync(
            j => j.Id == request.JobId && (j.State == JobState.Active || j.State == JobState.Pending),
            u =>
            {
                if (request.HasPercentCompleted)
                {
                    u.Set(
                        j => j.PercentCompleted,
                        Math.Round(request.PercentCompleted, 4, MidpointRounding.AwayFromZero)
                    );
                }
                if (request.HasMessage)
                    u.Set(j => j.Message, request.Message);
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
        string jobId = "";
        string engineId = "";
        List<Result> batch = [];
        await foreach (InsertResultsRequest request in requestStream.ReadAllAsync(context.CancellationToken))
        {
            if (jobId != request.JobId)
            {
                AssessmentJob? job = await _jobs.GetAsync(request.JobId, context.CancellationToken);
                if (job is null)
                    throw new RpcException(new Status(StatusCode.NotFound, "The job does not exist."));
                engineId = job.EngineRef;
                jobId = request.JobId;
            }

            batch.Add(
                new Result
                {
                    EngineRef = engineId,
                    JobRef = request.JobId,
                    TextId = request.TextId,
                    Ref = request.Ref,
                    Score = request.HasScore ? request.Score : null,
                    Description = request.HasDescription ? request.Description : null
                }
            );
            if (batch.Count == ResultInsertBatchSize)
            {
                await _results.InsertAllAsync(batch, context.CancellationToken);
                batch.Clear();
            }
        }
        if (batch.Count > 0)
            await _results.InsertAllAsync(batch, CancellationToken.None);

        return Empty;
    }
}
