using Serval.Engine.V1;

namespace Serval.Shared.Services;

public abstract class EngineServiceBase<TEngine, TBuild> : EngineServiceBase<TEngine, TBuild, IBuildResult>
    where TEngine : IEngine
    where TBuild : IBuild
{
    protected EngineServiceBase(
        IRepository<TEngine> engines,
        IRepository<TBuild> builds,
        GrpcClientFactory grpcClientFactory,
        IDataAccessContext dataAccessContext,
        ILoggerFactory loggerFactory
    )
        : base(engines, builds, null, grpcClientFactory, dataAccessContext, loggerFactory) { }
}

public abstract class EngineServiceBase<TEngine, TBuild, TResult>(
    IRepository<TEngine> engines,
    IRepository<TBuild> builds,
    IRepository<TResult>? results,
    GrpcClientFactory grpcClientFactory,
    IDataAccessContext dataAccessContext,
    ILoggerFactory loggerFactory
) : OwnedEntityServiceBase<TEngine>(engines)
    where TEngine : IEngine
    where TBuild : IBuild
    where TResult : IBuildResult
{
    protected readonly GrpcClientFactory GrpcClientFactory = grpcClientFactory;
    protected readonly IDataAccessContext DataAccessContext = dataAccessContext;
    protected readonly IRepository<TBuild> Builds = builds;
    protected readonly IRepository<TResult>? Results = results;
    private readonly ILogger<EngineServiceBase<TEngine, TBuild, TResult>> _logger = loggerFactory.CreateLogger<
        EngineServiceBase<TEngine, TBuild, TResult>
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
                await Builds.DeleteAllAsync(b => b.EngineRef == engineId, ct);
                if (Results is not null)
                    await Results.DeleteAllAsync(pt => pt.EngineRef == engineId, ct);
            },
            CancellationToken.None
        );
    }

    public async Task StartBuildAsync(
        TBuild build,
        string corpora_serialized,
        IReadOnlyDictionary<string, object>? options,
        CancellationToken cancellationToken = default
    )
    {
        TEngine engine = await GetAsync(build.EngineRef, cancellationToken);
        await Builds.InsertAsync(build, cancellationToken);

        try
        {
            EngineApi.EngineApiClient client = GrpcClientFactory.CreateClient<EngineApi.EngineApiClient>(engine.Type);
            var request = new StartBuildRequest
            {
                EngineType = engine.Type,
                EngineId = engine.Id,
                BuildId = build.Id,
                CorporaSerialized = corpora_serialized,
            };
            if (options is not null)
                request.OptionsSerialized = JsonSerializer.Serialize(options);

            // Log the build request summary
            try
            {
                var buildRequestSummary = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(request))!;
                // correct build options parsing
                buildRequestSummary.Remove("Options");
                try
                {
                    buildRequestSummary.Add("Options", JsonNode.Parse(request.OptionsSerialized));
                }
                catch (JsonException)
                {
                    buildRequestSummary.Add(
                        "Options",
                        "Build \"Options\" failed parsing: " + (request.OptionsSerialized ?? "null")
                    );
                }
                buildRequestSummary.Add("Event", "BuildRequest");
                buildRequestSummary.Add("ClientId", engine.Owner);
                _logger.LogInformation("{request}", buildRequestSummary.ToJsonString());
            }
            catch (JsonException)
            {
                _logger.LogInformation("Error parsing build request summary.");
                _logger.LogInformation("{request}", JsonSerializer.Serialize(request));
            }

            await client.StartBuildAsync(request, cancellationToken: cancellationToken);
        }
        catch
        {
            await Builds.DeleteAsync(build.Id, CancellationToken.None);
            throw;
        }
    }

    public async Task<bool> CancelBuildAsync(string engineId, CancellationToken cancellationToken = default)
    {
        TEngine? engine = await GetAsync(engineId, cancellationToken);
        if (engine is null)
            throw new EntityNotFoundException($"Could not find the Engine '{engineId}'.");

        EngineApi.EngineApiClient client = GrpcClientFactory.CreateClient<EngineApi.EngineApiClient>(engine.Type);
        try
        {
            await client.CancelBuildAsync(
                new CancelBuildRequest { EngineType = engine.Type, EngineId = engine.Id },
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
