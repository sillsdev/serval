namespace Serval.WordAlignment.Features.Engines;

public record WordAlignmentBuildConfigDto
{
    public string? Name { get; init; }
    public IReadOnlyList<TrainingCorpusConfigDto>? TrainOn { get; init; }
    public IReadOnlyList<WordAlignmentCorpusConfigDto>? WordAlignOn { get; init; }

    /// <example>
    /// {
    ///   "property" : "value"
    /// }
    /// </example>
    public object? Options { get; init; }
}

public record WordAlignmentCorpusConfigDto
{
    public string? ParallelCorpusId { get; init; }
    public IReadOnlyList<ParallelCorpusFilterConfigDto>? SourceFilters { get; init; }
    public IReadOnlyList<ParallelCorpusFilterConfigDto>? TargetFilters { get; init; }
}

public record StartBuild(string Owner, string EngineId, WordAlignmentBuildConfigDto BuildConfig)
    : IRequest<StartBuildResponse>;

public record StartBuildResponse(
    [property: MemberNotNullWhen(false, nameof(Build))] bool IsBuildRunning,
    WordAlignmentBuildDto? Build = null
);

public class StartBuildHandler(
    IDataAccessContext dataAccessContext,
    IRepository<Engine> engines,
    IRepository<Build> builds,
    ContractMapper contractMapper,
    IEngineServiceFactory engineFactory,
    ILogger<StartBuildHandler> logger,
    DtoMapper dtoMapper,
    IConfiguration configuration
) : IRequestHandler<StartBuild, StartBuildResponse>
{
    private static readonly JsonSerializerOptions ObjectJsonSerializerOptions = new()
    {
        Converters = { new ObjectToInferredTypesConverter() },
    };

    public Task<StartBuildResponse> HandleAsync(StartBuild request, CancellationToken cancellationToken = default)
    {
        return dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                Engine engine = await engines.CheckOwnerAsync(request.EngineId, request.Owner, ct);

                if (
                    await builds.ExistsAsync(
                        b =>
                            b.EngineRef == request.EngineId
                            && (b.State == JobState.Active || b.State == JobState.Pending),
                        ct
                    )
                )
                {
                    return new StartBuildResponse(IsBuildRunning: true);
                }

                Build build = new()
                {
                    EngineRef = engine.Id,
                    WordAlignOn = Map(engine, request.BuildConfig.WordAlignOn),
                    TrainOn = Map(engine, request.BuildConfig.TrainOn),
                    Options = MapOptions(request.BuildConfig.Options),
                    DeploymentVersion = configuration.GetValue<string>("deploymentVersion") ?? "Unknown",
                    DateCreated = DateTime.UtcNow,
                };
                await builds.InsertAsync(build, ct);

                IReadOnlyList<ParallelCorpusContract> corpora = contractMapper.Map(build, engine);

                string? buildOptions = null;
                if (build.Options is not null)
                    buildOptions = JsonSerializer.Serialize(build.Options);
                try
                {
                    var buildRequestSummary = new JsonObject
                    {
                        ["Event"] = "BuildRequest",
                        ["EngineId"] = engine.Id,
                        ["BuildId"] = build.Id,
                        ["CorpusCount"] = corpora.Count,
                        ["ModelRevision"] = engine.ModelRevision,
                        ["ClientId"] = engine.Owner,
                    };
                    string? buildOptionsStr = null;
                    try
                    {
                        buildRequestSummary.Add("Options", JsonNode.Parse(buildOptions ?? "null"));
                    }
                    catch (JsonException)
                    {
                        buildRequestSummary.Add(
                            "Options",
                            "Build \"Options\" failed parsing: " + (buildOptionsStr ?? "null")
                        );
                    }
                    logger.LogInformation("{request}", buildRequestSummary.ToJsonString());
                }
                catch (JsonException)
                {
                    logger.LogInformation("Error parsing build request summary.");
                }

                await engineFactory
                    .GetEngineService(engine.Type)
                    .StartBuildAsync(engine.Id, build.Id, corpora, buildOptions, ct);
                return new StartBuildResponse(IsBuildRunning: false, Build: dtoMapper.Map(build));
            },
            cancellationToken
        );
    }

    private static List<WordAlignmentCorpus>? Map(Engine engine, IReadOnlyList<WordAlignmentCorpusConfigDto>? source)
    {
        if (source is null)
            return null;

        if (
            source.Where(p => p.ParallelCorpusId != null).Select(p => p.ParallelCorpusId).Distinct().Count()
            != source.Count(p => p.ParallelCorpusId != null)
        )
        {
            throw new InvalidOperationException($"Each ParallelCorpusId may only be specified once.");
        }

        var parallelCorpusIds = new HashSet<string>(engine.ParallelCorpora.Select(c => c.Id));
        var wordAlignOnCorpora = new List<WordAlignmentCorpus>();
        foreach (WordAlignmentCorpusConfigDto wcc in source)
        {
            if (wcc.ParallelCorpusId == null)
            {
                throw new InvalidOperationException($"ParallelCorpusId must be set.");
            }
            if (!parallelCorpusIds.Contains(wcc.ParallelCorpusId))
            {
                throw new InvalidOperationException(
                    $"The parallel corpus {wcc.ParallelCorpusId} is not valid: This parallel corpus does not exist for engine {engine.Id}."
                );
            }
            ParallelCorpus corpus = engine.ParallelCorpora.Single(pc => pc.Id == wcc.ParallelCorpusId);
            if (corpus.SourceCorpora.Count == 0 && corpus.TargetCorpora.Count == 0)
            {
                throw new InvalidOperationException(
                    $"The corpus {wcc.ParallelCorpusId} does not have source or target corpora associated with it."
                );
            }
            if (
                wcc.SourceFilters != null
                && wcc.SourceFilters.Count > 0
                && (
                    wcc.SourceFilters.Select(sf => sf.CorpusId).Distinct().Count() > 1
                    || wcc.SourceFilters[0].CorpusId
                        != engine.ParallelCorpora.Single(pc => pc.Id == wcc.ParallelCorpusId).SourceCorpora[0].Id
                )
            )
            {
                throw new InvalidOperationException(
                    $"Only the first source corpus in a parallel corpus may be filtered for pretranslation."
                );
            }
            wordAlignOnCorpora.Add(
                new WordAlignmentCorpus
                {
                    ParallelCorpusRef = wcc.ParallelCorpusId,
                    SourceFilters = wcc.SourceFilters?.Select(Map).ToList(),
                    TargetFilters = wcc.TargetFilters?.Select(Map).ToList(),
                }
            );
        }

        return wordAlignOnCorpora;
    }

#pragma warning disable CS0612 // Type or member is obsolete

    private static List<TrainingCorpus>? Map(Engine engine, IReadOnlyList<TrainingCorpusConfigDto>? source)
    {
        if (source is null)
            return null;

        if (
            source.Where(p => p.ParallelCorpusId != null).Select(p => p.ParallelCorpusId).Distinct().Count()
            != source.Count(p => p.ParallelCorpusId != null)
        )
        {
            throw new InvalidOperationException($"Each ParallelCorpusId may only be specified once.");
        }

        var parallelCorpusIds = new HashSet<string>(engine.ParallelCorpora.Select(c => c.Id));
        var trainOnCorpora = new List<TrainingCorpus>();
        foreach (TrainingCorpusConfigDto tcc in source)
        {
            if (tcc.CorpusId != null)
            {
                throw new InvalidOperationException($"CorpusId cannot be set. Only ParallelCorpusId is supported.");
            }
            if (tcc.ParallelCorpusId == null)
            {
                throw new InvalidOperationException($"One of ParallelCorpusId and CorpusId must be set.");
            }
            if (!parallelCorpusIds.Contains(tcc.ParallelCorpusId))
            {
                throw new InvalidOperationException(
                    $"The parallel corpus {tcc.ParallelCorpusId} is not valid: This parallel corpus does not exist for engine {engine.Id}."
                );
            }
            ParallelCorpus corpus = engine.ParallelCorpora.Single(pc => pc.Id == tcc.ParallelCorpusId);
            if (corpus.SourceCorpora.Count == 0 && corpus.TargetCorpora.Count == 0)
            {
                throw new InvalidOperationException(
                    $"The corpus {tcc.ParallelCorpusId} does not have source or target corpora associated with it."
                );
            }
            foreach (MonolingualCorpus monolingualCorpus in corpus.SourceCorpora.Concat(corpus.TargetCorpora))
            {
                if (monolingualCorpus.Files.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"The corpus {monolingualCorpus.Id} referenced in parallel corpus {corpus.Id} does not have any files associated with it."
                    );
                }
            }
            trainOnCorpora.Add(
                new TrainingCorpus
                {
                    ParallelCorpusRef = tcc.ParallelCorpusId,
                    SourceFilters = tcc.SourceFilters?.Select(Map).ToList(),
                    TargetFilters = tcc.TargetFilters?.Select(Map).ToList(),
                }
            );
        }
        return trainOnCorpora;
    }
#pragma warning restore CS0612 // Type or member is obsolete

    private static ParallelCorpusFilter Map(ParallelCorpusFilterConfigDto source)
    {
        if (source.TextIds != null && source.ScriptureRange != null)
        {
            throw new InvalidOperationException(
                $"The parallel corpus filter for corpus {source.CorpusId} is not valid: At most, one of TextIds and ScriptureRange can be set."
            );
        }
        return new ParallelCorpusFilter
        {
            CorpusRef = source.CorpusId,
            TextIds = source.TextIds,
            ScriptureRange = source.ScriptureRange,
        };
    }

    private static Dictionary<string, object>? MapOptions(object? source)
    {
        if (source is null)
            return null;
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(
                source.ToString()!,
                ObjectJsonSerializerOptions
            );
        }
        catch (Exception e)
        {
            throw new InvalidOperationException($"Unable to parse field 'options' : {e.Message}", e);
        }
    }
}

public partial class WordAlignmentEnginesController
{
    /// <summary>
    /// Starts a build job for a engine.
    /// </summary>
    /// <remarks>
    /// Specify the corpora and textIds to train on. If no `trainOn` field is provided, all corpora will be used. Only parallel corpora are supported.
    /// Paratext projects can be filtered by [book using the `textIds`](https://github.com/sillsdev/libpalaso/blob/master/SIL.Scripture/Canon.cs).
    /// Filters can also be supplied via `scriptureRange` parameter as ranges of biblical text. See [here](https://github.com/sillsdev/serval/wiki/Filtering-Paratext-Project-Data-with-a-Scripture-Range)
    /// All Paratext project filtering follows original versification. See [here](https://github.com/sillsdev/serval/wiki/Versification-in-Serval) for more information.
    ///
    /// Specify the corpora or text ids to word align on.
    /// When a corpus or text id is selected for word align on, only text segments that are in both the source and the target will be aligned.
    ///
    /// The `options` parameter of the build config provides the ability to pass build configuration parameters as a JSON object.
    /// See [statistical alignment job settings documentation](https://github.com/sillsdev/serval/wiki/Statistical-Alignment-Build-Options) about configuring job parameters.
    /// </remarks>
    /// <param name="id">The engine id</param>
    /// <param name="buildConfig">The build config (see remarks)</param>
    /// <param name="cancellationToken"></param>
    /// <response code="201">The new build job</response>
    /// <response code="400">The build configuration was invalid.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client does not own the engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="409">There is already an active/pending build or a build in the process of being canceled.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.UpdateWordAlignmentEngines)]
    [HttpPost("{id}/builds")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<WordAlignmentBuildDto>> StartBuildAsync(
        [NotNull] string id,
        [FromBody] WordAlignmentBuildConfigDto buildConfig,
        [FromServices] IRequestHandler<StartBuild, StartBuildResponse> handler,
        CancellationToken cancellationToken
    )
    {
        StartBuildResponse response = await handler.HandleAsync(new(Owner, id, buildConfig), cancellationToken);

        if (response.IsBuildRunning)
            return Conflict();

        return Created(response.Build.Url, response.Build);
    }
}
