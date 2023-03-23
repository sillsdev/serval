using Google.Protobuf.WellKnownTypes;
using Serval.Translation.V1;

namespace Serval.Translation.Services;

public class TranslationPlatformServiceV1 : TranslationPlatformApi.TranslationPlatformApiBase
{
    private const int PretranslationInsertBatchSize = 128;
    private static readonly Empty Empty = new();

    private readonly IRepository<Build> _builds;
    private readonly IRepository<TranslationEngine> _engines;
    private readonly IRepository<Pretranslation> _pretranslations;
    private readonly IDataAccessContext _dataAccessContext;
    private readonly IScopedMediator _mediator;

    public TranslationPlatformServiceV1(
        IRepository<Build> builds,
        IRepository<TranslationEngine> engines,
        IRepository<Pretranslation> pretranslations,
        IDataAccessContext dataAccessContext,
        IScopedMediator mediator
    )
    {
        _builds = builds;
        _engines = engines;
        _pretranslations = pretranslations;
        _dataAccessContext = dataAccessContext;
        _mediator = mediator;
    }

    public override async Task<Empty> BuildStarted(BuildStartedRequest request, ServerCallContext context)
    {
        await _dataAccessContext.BeginTransactionAsync(context.CancellationToken);
        Build? build = await _builds.UpdateAsync(
            request.BuildId,
            u => u.Set(b => b.State, BuildState.Active),
            cancellationToken: context.CancellationToken
        );
        if (build is null)
            throw new RpcException(new Status(StatusCode.NotFound, "The build does not exist."));

        TranslationEngine? engine = await _engines.UpdateAsync(
            build.EngineRef,
            u => u.Set(e => e.IsBuilding, true),
            cancellationToken: context.CancellationToken
        );
        if (engine is null)
            throw new RpcException(new Status(StatusCode.NotFound, "The engine does not exist."));

        await _mediator.Publish(
            new BuildStarted
            {
                BuildId = build.Id,
                EngineId = engine.Id,
                Owner = engine.Owner
            },
            context.CancellationToken
        );
        await _dataAccessContext.CommitTransactionAsync(CancellationToken.None);

        return Empty;
    }

    public override async Task<Empty> BuildCompleted(BuildCompletedRequest request, ServerCallContext context)
    {
        await _dataAccessContext.BeginTransactionAsync(context.CancellationToken);
        Build? build = await _builds.UpdateAsync(
            request.BuildId,
            u =>
                u.Set(b => b.State, BuildState.Completed)
                    .Set(b => b.Message, "Completed")
                    .Set(b => b.DateFinished, DateTime.UtcNow),
            cancellationToken: context.CancellationToken
        );
        if (build is null)
            throw new RpcException(new Status(StatusCode.NotFound, "The build does not exist."));

        TranslationEngine? engine = await _engines.UpdateAsync(
            build.EngineRef,
            u =>
                u.Set(e => e.Confidence, request.Confidence)
                    .Set(e => e.CorpusSize, request.CorpusSize)
                    .Set(e => e.IsBuilding, false)
                    .Inc(e => e.ModelRevision),
            cancellationToken: context.CancellationToken
        );
        if (engine is null)
            throw new RpcException(new Status(StatusCode.NotFound, "The engine does not exist."));

        await _mediator.Publish(
            new BuildFinished
            {
                BuildId = build.Id,
                EngineId = engine.Id,
                Owner = engine.Owner,
                BuildState = build.State,
                Message = build.Message!,
                DateFinished = build.DateFinished!.Value
            },
            context.CancellationToken
        );
        await _dataAccessContext.CommitTransactionAsync(CancellationToken.None);

        return Empty;
    }

    public override async Task<Empty> BuildCanceled(BuildCanceledRequest request, ServerCallContext context)
    {
        await _dataAccessContext.BeginTransactionAsync(context.CancellationToken);
        Build? build = await _builds.UpdateAsync(
            request.BuildId,
            u => u.Set(b => b.Message, "Canceled").Set(b => b.DateFinished, DateTime.UtcNow),
            cancellationToken: context.CancellationToken
        );
        if (build is null)
            throw new RpcException(new Status(StatusCode.NotFound, "The build does not exist."));

        TranslationEngine? engine = await _engines.UpdateAsync(
            build.EngineRef,
            u => u.Set(e => e.IsBuilding, false),
            cancellationToken: context.CancellationToken
        );
        if (engine is null)
            throw new RpcException(new Status(StatusCode.NotFound, "The engine does not exist."));

        await _mediator.Publish(
            new BuildFinished
            {
                BuildId = build.Id,
                EngineId = engine.Id,
                Owner = engine.Owner,
                BuildState = build.State,
                Message = build.Message!,
                DateFinished = build.DateFinished!.Value
            },
            context.CancellationToken
        );
        await _dataAccessContext.CommitTransactionAsync(CancellationToken.None);

        return Empty;
    }

    public override async Task<Empty> BuildFaulted(BuildFaultedRequest request, ServerCallContext context)
    {
        await _dataAccessContext.BeginTransactionAsync(context.CancellationToken);
        Build? build = await _builds.UpdateAsync(
            request.BuildId,
            u =>
                u.Set(b => b.State, BuildState.Faulted)
                    .Set(b => b.Message, request.Message)
                    .Set(b => b.DateFinished, DateTime.UtcNow),
            cancellationToken: context.CancellationToken
        );
        if (build is null)
            throw new RpcException(new Status(StatusCode.NotFound, "The build does not exist."));

        TranslationEngine? engine = await _engines.UpdateAsync(
            build.EngineRef,
            u => u.Set(e => e.IsBuilding, false),
            cancellationToken: context.CancellationToken
        );
        if (engine is null)
            throw new RpcException(new Status(StatusCode.NotFound, "The engine does not exist."));

        await _mediator.Publish(
            new BuildFinished
            {
                BuildId = build.Id,
                EngineId = engine.Id,
                Owner = engine.Owner,
                BuildState = build.State,
                Message = build.Message!,
                DateFinished = build.DateFinished!.Value
            },
            context.CancellationToken
        );
        await _dataAccessContext.CommitTransactionAsync(CancellationToken.None);

        return Empty;
    }

    public override async Task<Empty> BuildRestarting(BuildRestartingRequest request, ServerCallContext context)
    {
        Build? build = await _builds.UpdateAsync(
            request.BuildId,
            u =>
                u.Set(b => b.Message, "Restarting")
                    .Set(b => b.Step, 0)
                    .Set(b => b.PercentCompleted, 0)
                    .Set(b => b.State, BuildState.Pending),
            cancellationToken: context.CancellationToken
        );
        if (build is null)
            throw new RpcException(new Status(StatusCode.NotFound, "The build does not exist."));

        return Empty;
    }

    public override async Task<Empty> UpdateBuildStatus(UpdateBuildStatusRequest request, ServerCallContext context)
    {
        await _builds.UpdateAsync(
            b => b.Id == request.BuildId && b.State == BuildState.Active,
            u =>
            {
                u.Set(b => b.Step, request.Step);
                if (request.HasPercentCompleted)
                {
                    u.Set(
                        b => b.PercentCompleted,
                        Math.Round(request.PercentCompleted, 4, MidpointRounding.AwayFromZero)
                    );
                }
                if (request.HasMessage)
                    u.Set(b => b.Message, request.Message);
            },
            cancellationToken: context.CancellationToken
        );

        return Empty;
    }

    public override async Task<Empty> IncrementTranslationEngineCorpusSize(
        IncrementTranslationEngineCorpusSizeRequest request,
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

    public override async Task<Empty> DeleteAllPretranslations(
        DeleteAllPretranslationsRequest request,
        ServerCallContext context
    )
    {
        await _pretranslations.DeleteAllAsync(p => p.EngineRef == request.EngineId, context.CancellationToken);
        return Empty;
    }

    public override async Task<Empty> InsertPretranslations(
        IAsyncStreamReader<InsertPretranslationRequest> requestStream,
        ServerCallContext context
    )
    {
        var batch = new List<Pretranslation>();
        await foreach (InsertPretranslationRequest request in requestStream.ReadAllAsync(context.CancellationToken))
        {
            batch.Add(
                new Pretranslation
                {
                    EngineRef = request.EngineId,
                    CorpusRef = request.CorpusId,
                    TextId = request.TextId,
                    Refs = request.Refs.ToList(),
                    Translation = request.Translation,
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
}
