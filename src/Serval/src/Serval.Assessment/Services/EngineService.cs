using Serval.Assessment.V1;

namespace Serval.Assessment.Services;

public class EngineService(
    IRepository<Engine> engines,
    IRepository<Job> jobs,
    IRepository<Result> results,
    GrpcClientFactory grpcClientFactory,
    IOptionsMonitor<DataFileOptions> dataFileOptions,
    IDataAccessContext dataAccessContext,
    ILoggerFactory loggerFactory,
    IScriptureDataFileService scriptureDataFileService
) : OwnedEntityServiceBase<Engine>(engines), IEngineService
{
    private readonly IRepository<Job> _jobs = jobs;
    private readonly IRepository<Result> _results = results;
    private readonly GrpcClientFactory _grpcClientFactory = grpcClientFactory;
    private readonly IOptionsMonitor<DataFileOptions> _dataFileOptions = dataFileOptions;
    private readonly IDataAccessContext _dataAccessContext = dataAccessContext;
    private readonly ILogger<EngineService> _logger = loggerFactory.CreateLogger<EngineService>();
    private readonly IScriptureDataFileService _scriptureDataFileService = scriptureDataFileService;

    public override async Task<Engine> CreateAsync(Engine engine, CancellationToken cancellationToken = default)
    {
        try
        {
            await Entities.InsertAsync(engine, cancellationToken);
            var client = _grpcClientFactory.CreateClient<AssessmentEngineApi.AssessmentEngineApiClient>(engine.Type);
            if (client is null)
                throw new InvalidOperationException($"'{engine.Type}' is an invalid engine type.");
            var request = new CreateRequest { EngineType = engine.Type, EngineId = engine.Id, };
            if (engine.Name is not null)
                request.EngineName = engine.Name;
            await client.CreateAsync(request, cancellationToken: cancellationToken);
        }
        catch
        {
            await Entities.DeleteAsync(engine, CancellationToken.None);
            throw;
        }
        return engine;
    }

    public override async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        Engine? engine = await Entities.GetAsync(id, cancellationToken);
        if (engine is null)
            throw new EntityNotFoundException($"Could not find the Engine '{id}'.");

        var client = _grpcClientFactory.CreateClient<AssessmentEngineApi.AssessmentEngineApiClient>(engine.Type);
        await client.DeleteAsync(
            new DeleteRequest { EngineType = engine.Type, EngineId = engine.Id },
            cancellationToken: cancellationToken
        );

        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                await Entities.DeleteAsync(id, ct);
                await _jobs.DeleteAllAsync(b => b.EngineRef == id, ct);
                await _results.DeleteAllAsync(r => r.EngineRef == id, ct);
            },
            CancellationToken.None
        );
    }

    public async Task<Models.Corpus> ReplaceCorpusAsync(
        string id,
        Models.Corpus corpus,
        CancellationToken cancellationToken = default
    )
    {
        Engine? engine = await Entities.UpdateAsync(
            id,
            u => u.Set(e => e.Corpus, corpus),
            cancellationToken: cancellationToken
        );
        if (engine is null)
            throw new EntityNotFoundException($"Could not find the Engine '{id}'.");
        return engine.Corpus;
    }

    public async Task<Models.Corpus> ReplaceReferenceCorpusAsync(
        string id,
        Models.Corpus referenceCorpus,
        CancellationToken cancellationToken = default
    )
    {
        Engine? engine = await Entities.UpdateAsync(
            id,
            u => u.Set(e => e.ReferenceCorpus, referenceCorpus),
            cancellationToken: cancellationToken
        );
        if (engine is null)
            throw new EntityNotFoundException($"Could not find the Engine '{id}'.");
        return engine.ReferenceCorpus!;
    }

    public async Task StartJobAsync(Job job, CancellationToken cancellationToken = default)
    {
        Engine engine = await GetAsync(job.EngineRef, cancellationToken);
        await _jobs.InsertAsync(job, cancellationToken);

        try
        {
            AssessmentEngineApi.AssessmentEngineApiClient client =
                _grpcClientFactory.CreateClient<AssessmentEngineApi.AssessmentEngineApiClient>(engine.Type);
            var request = new StartJobRequest
            {
                EngineType = engine.Type,
                EngineId = engine.Id,
                JobId = job.Id,
                Options = JsonSerializer.Serialize(job.Options),
                Corpus = Map(engine.Corpus),
                IncludeAll = job.TextIds is null || job.TextIds.Count == 0
            };
            if (engine.ReferenceCorpus is not null)
                request.ReferenceCorpus = Map(engine.ReferenceCorpus);
            if (job.TextIds is not null)
                request.IncludeTextIds.Add(job.TextIds);
            if (job.ScriptureRange is not null)
            {
                if (
                    engine.Corpus.Files.Count > 1
                    || engine.Corpus.Files[0].Format != Shared.Contracts.FileFormat.Paratext
                )
                {
                    throw new InvalidOperationException($"The engine is not compatible with using a scripture range.");
                }

                try
                {
                    ScrVers versification = _scriptureDataFileService
                        .GetParatextProjectSettings(request.Corpus.Files[0].Location)
                        .Versification;
                    Dictionary<string, ScriptureChapters> chapters = ScriptureRangeParser
                        .GetChapters(job.ScriptureRange, versification)
                        .ToDictionary(kvp => kvp.Key, kvp => new ScriptureChapters { Chapters = { kvp.Value } });
                    request.IncludeChapters.Add(chapters);
                }
                catch (ArgumentException ae)
                {
                    throw new InvalidOperationException(
                        $"The scripture range {job.ScriptureRange} is not valid: {ae.Message}"
                    );
                }
            }

            // Log the job request summary
            try
            {
                var jobRequestSummary = (JsonObject)JsonNode.Parse(JsonSerializer.Serialize(request))!;
                // correct job options parsing
                jobRequestSummary.Remove("Options");
                try
                {
                    jobRequestSummary.Add("Options", JsonNode.Parse(request.Options));
                }
                catch (JsonException)
                {
                    jobRequestSummary.Add("Options", "Job \"Options\" failed parsing: " + (request.Options ?? "null"));
                }
                jobRequestSummary.Add("Event", "JobRequest");
                jobRequestSummary.Add("ClientId", engine.Owner);
                _logger.LogInformation("{request}", jobRequestSummary.ToJsonString());
            }
            catch (JsonException)
            {
                _logger.LogInformation("Error parsing job request summary.");
                _logger.LogInformation("{request}", JsonSerializer.Serialize(request));
            }

            await client.StartJobAsync(request, cancellationToken: cancellationToken);
        }
        catch
        {
            await _jobs.DeleteAsync(job, CancellationToken.None);
            throw;
        }
    }

    public async Task<bool> CancelJobAsync(string id, string jobId, CancellationToken cancellationToken = default)
    {
        Engine? engine = await GetAsync(id, cancellationToken);
        if (engine is null)
            throw new EntityNotFoundException($"Could not find the Engine '{id}'.");

        AssessmentEngineApi.AssessmentEngineApiClient client =
            _grpcClientFactory.CreateClient<AssessmentEngineApi.AssessmentEngineApiClient>(engine.Type);
        try
        {
            await client.CancelJobAsync(
                new CancelJobRequest
                {
                    EngineType = engine.Type,
                    EngineId = engine.Id,
                    JobId = jobId
                },
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

    public Task DeleteAllCorpusFilesAsync(string dataFileId, CancellationToken cancellationToken = default)
    {
        return Entities.UpdateAllAsync(
            e => e.Corpus.Files.Any(f => f.Id == dataFileId) || e.ReferenceCorpus!.Files.Any(f => f.Id == dataFileId),
            u =>
            {
                u.RemoveAll(e => e.Corpus.Files, f => f.Id == dataFileId);
                u.RemoveAll(e => e.ReferenceCorpus!.Files, f => f.Id == dataFileId);
            },
            cancellationToken
        );
    }

    private V1.Corpus Map(Models.Corpus source)
    {
        return new V1.Corpus { Language = source.Language, Files = { source.Files.Select(Map) } };
    }

    private V1.CorpusFile Map(Shared.Models.CorpusFile source)
    {
        return new V1.CorpusFile
        {
            TextId = source.TextId,
            Format = (V1.FileFormat)source.Format,
            Location = Path.Combine(_dataFileOptions.CurrentValue.FilesDirectory, source.Filename)
        };
    }
}
