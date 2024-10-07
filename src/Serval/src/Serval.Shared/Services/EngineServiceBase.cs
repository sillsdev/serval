using Serval.Engine.V1;

namespace Serval.Shared.Services;

public abstract class EngineServiceBase<TEngine, TJob> : EngineServiceBase<TEngine, TJob, IJobResult>
    where TEngine : IEngine
    where TJob : IJob
{
    protected EngineServiceBase(
        IRepository<TEngine> engines,
        IRepository<TJob> jobs,
        GrpcClientFactory grpcClientFactory,
        IDataAccessContext dataAccessContext,
        ILoggerFactory loggerFactory
    )
        : base(engines, jobs, null, grpcClientFactory, dataAccessContext, loggerFactory) { }
}

public abstract class EngineServiceBase<TEngine, TJob, TResult>(
    IRepository<TEngine> engines,
    IRepository<TJob> jobs,
    IRepository<TResult>? results,
    GrpcClientFactory grpcClientFactory,
    IDataAccessContext dataAccessContext,
    ILoggerFactory loggerFactory
) : OwnedEntityServiceBase<TEngine>(engines)
    where TEngine : IEngine
    where TJob : IJob
    where TResult : IJobResult
{
    protected readonly GrpcClientFactory GrpcClientFactory = grpcClientFactory;
    protected readonly IDataAccessContext DataAccessContext = dataAccessContext;
    protected readonly IRepository<TJob> Jobs = jobs;
    protected readonly IRepository<TResult>? Results = results;
    private readonly ILogger<EngineServiceBase<TEngine, TJob, TResult>> _logger = loggerFactory.CreateLogger<
        EngineServiceBase<TEngine, TJob, TResult>
    >();

    public virtual async Task<string> CreateAsync(
        TEngine engine,
        string parameters_serialized,
        CancellationToken cancellationToken = default
    )
    {
        CreateResponse createResponse;
        try
        {
            await Entities.InsertAsync(engine, cancellationToken);
            EngineApi.EngineApiClient? client = GrpcClientFactory.CreateClient<EngineApi.EngineApiClient>(engine.Type);
            if (client is null)
                throw new InvalidOperationException($"'{engine.Type}' is an invalid engine type.");
            var request = new CreateRequest
            {
                EngineType = engine.Type,
                EngineId = engine.Id,
                ParametersSerialized = parameters_serialized
            };

            if (engine.Name is not null)
                request.EngineName = engine.Name;
            createResponse = await client.CreateAsync(request, cancellationToken: cancellationToken);
            // IsModelPersisted may be updated by the engine with the respective default.
        }
        catch (RpcException rpcex)
        {
            await Entities.DeleteAsync(engine.Id, CancellationToken.None);
            if (rpcex.StatusCode == StatusCode.InvalidArgument)
            {
                throw new InvalidOperationException(
                    $"Unable to create engine {engine.Id} because of an invalid argument: {rpcex.Status.Detail}",
                    rpcex
                );
            }
            throw;
        }
        catch
        {
            await Entities.DeleteAsync(engine.Id, CancellationToken.None);
            throw;
        }
        return createResponse.ResultsSerialized;
    }

    public override async Task DeleteAsync(string engineId, CancellationToken cancellationToken = default)
    {
        TEngine? engine = await Entities.GetAsync(engineId, cancellationToken);
        if (engine is null)
            throw new EntityNotFoundException($"Could not find the Engine '{engineId}'.");

        EngineApi.EngineApiClient client = GrpcClientFactory.CreateClient<EngineApi.EngineApiClient>(engine.Type);
        await client.DeleteAsync(
            new DeleteRequest { EngineType = engine.Type, EngineId = engine.Id },
            cancellationToken: cancellationToken
        );

        await DataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                await Entities.DeleteAsync(engineId, ct);
                await Jobs.DeleteAllAsync(b => b.EngineRef == engineId, ct);
                if (Results is not null)
                    await Results.DeleteAllAsync(pt => pt.EngineRef == engineId, ct);
            },
            CancellationToken.None
        );
    }

    public async Task StartJobAsync(
        TJob job,
        string corpora_serialized,
        IReadOnlyDictionary<string, object>? options,
        CancellationToken cancellationToken = default
    )
    {
        TEngine engine = await GetAsync(job.EngineRef, cancellationToken);
        await Jobs.InsertAsync(job, cancellationToken);

        try
        {
            EngineApi.EngineApiClient client = GrpcClientFactory.CreateClient<EngineApi.EngineApiClient>(engine.Type);
            var request = new StartJobRequest
            {
                EngineType = engine.Type,
                EngineId = engine.Id,
                JobId = job.Id,
                CorporaSerialized = corpora_serialized,
            };
            if (options is not null)
                request.OptionsSerialized = JsonSerializer.Serialize(options);

            // Log the build request summary
            try
            {
                var jobRequestSummary = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(request))!;
                // correct build options parsing
                jobRequestSummary.Remove("Options");
                try
                {
                    jobRequestSummary.Add("Options", JsonNode.Parse(request.OptionsSerialized));
                }
                catch (JsonException)
                {
                    jobRequestSummary.Add(
                        "Options",
                        "Build \"Options\" failed parsing: " + (request.OptionsSerialized ?? "null")
                    );
                }
                jobRequestSummary.Add("Event", "JobRequest");
                jobRequestSummary.Add("ClientId", engine.Owner);
                _logger.LogInformation("{request}", jobRequestSummary.ToJsonString());
            }
            catch (JsonException)
            {
                _logger.LogInformation("Error parsing build request summary.");
                _logger.LogInformation("{request}", JsonSerializer.Serialize(request));
            }

            await client.StartJobAsync(request, cancellationToken: cancellationToken);
        }
        catch
        {
            await Jobs.DeleteAsync(job.Id, CancellationToken.None);
            throw;
        }
    }

    public async Task<bool> CancelJobAsync(string engineId, CancellationToken cancellationToken = default)
    {
        TEngine? engine = await GetAsync(engineId, cancellationToken);
        if (engine is null)
            throw new EntityNotFoundException($"Could not find the Engine '{engineId}'.");

        EngineApi.EngineApiClient client = GrpcClientFactory.CreateClient<EngineApi.EngineApiClient>(engine.Type);
        try
        {
            await client.CancelJobAsync(
                new CancelJobRequest { EngineType = engine.Type, EngineId = engine.Id },
                cancellationToken: cancellationToken
            );
        }
        catch (RpcException re)
        {
            if (re.StatusCode is StatusCode.Aborted)
                return false;
            throw;
        }
        return true;
    }

    public async Task<Queue> GetQueueAsync(string engineType, CancellationToken cancellationToken = default)
    {
        EngineApi.EngineApiClient client = GrpcClientFactory.CreateClient<EngineApi.EngineApiClient>(engineType);
        GetQueueSizeResponse response = await client.GetQueueSizeAsync(
            new GetQueueSizeRequest { EngineType = engineType },
            cancellationToken: cancellationToken
        );
        return new Queue { Size = response.Size, EngineType = engineType };
    }
}
