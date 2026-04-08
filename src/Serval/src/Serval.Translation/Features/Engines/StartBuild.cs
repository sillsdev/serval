namespace Serval.Translation.Features.Engines;

public record TranslationBuildConfigDto
{
    public string? Name { get; init; }
    public IReadOnlyList<TrainingCorpusConfigDto>? TrainOn { get; init; }
    public IReadOnlyList<PretranslateCorpusConfigDto>? Pretranslate { get; init; }

    /// <example>
    /// {
    ///   "property" : "value"
    /// }
    /// </example>
    public object? Options { get; init; }
}

public record StartBuild(string Owner, string EngineId, TranslationBuildConfigDto BuildConfig)
    : IRequest<StartBuildResponse>;

public record struct StartBuildResponse(
    [property: MemberNotNullWhen(false, nameof(Build))] bool IsBuildRunning,
    TranslationBuildDto? Build = null
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
                Engine? engine = await engines.GetAsync(request.EngineId, ct);
                if (engine is null)
                    throw new EntityNotFoundException($"Could not find the Engine '{request.EngineId}'.");
                if (engine.Owner != request.Owner)
                    throw new ForbiddenException();

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
                    Owner = engine.Owner,
                    Name = request.BuildConfig.Name,
                    Pretranslate = Map(engine, request.BuildConfig.Pretranslate),
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

#pragma warning disable CS0612 // Type or member is obsolete
    private static List<PretranslateCorpus>? Map(Engine engine, IReadOnlyList<PretranslateCorpusConfigDto>? source)
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

        if (
            source.Where(p => p.CorpusId != null).Select(p => p.CorpusId).Distinct().Count()
            != source.Count(p => p.CorpusId != null)
        )
        {
            throw new InvalidOperationException($"Each CorpusId may only be specified once.");
        }

        var corpusIds = new HashSet<string>(engine.Corpora.Select(c => c.Id));
        var parallelCorpusIds = new HashSet<string>(engine.ParallelCorpora.Select(c => c.Id));
        var pretranslateCorpora = new List<PretranslateCorpus>();
        foreach (PretranslateCorpusConfigDto pcc in source)
        {
            if (pcc.CorpusId != null)
            {
                if (pcc.ParallelCorpusId != null)
                {
                    throw new InvalidOperationException($"Only one of ParallelCorpusId and CorpusId can be set.");
                }
                if (!corpusIds.Contains(pcc.CorpusId))
                {
                    throw new InvalidOperationException(
                        $"The corpus {pcc.CorpusId} is not valid: This corpus does not exist for engine {engine.Id}."
                    );
                }
                Corpus corpus = engine.Corpora.Single(c => c.Id == pcc.CorpusId);
                if (corpus.SourceFiles.Count == 0 && corpus.TargetFiles.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"The corpus {pcc.CorpusId} is not valid: This corpus does not have any source or target files."
                    );
                }
                if (pcc.TextIds != null && pcc.ScriptureRange != null)
                {
                    throw new InvalidOperationException(
                        $"The corpus {pcc.CorpusId} is not valid: Set at most one of TextIds and ScriptureRange."
                    );
                }
                pretranslateCorpora.Add(
                    new PretranslateCorpus
                    {
                        CorpusRef = pcc.CorpusId,
                        TextIds = pcc.TextIds?.ToList(),
                        ScriptureRange = pcc.ScriptureRange,
                    }
                );
            }
            else
            {
                if (pcc.ParallelCorpusId == null)
                {
                    throw new InvalidOperationException($"One of ParallelCorpusId and CorpusId must be set.");
                }
                if (!parallelCorpusIds.Contains(pcc.ParallelCorpusId))
                {
                    throw new InvalidOperationException(
                        $"The parallel corpus {pcc.ParallelCorpusId} is not valid: This parallel corpus does not exist for engine {engine.Id}."
                    );
                }
                ParallelCorpus corpus = engine.ParallelCorpora.Single(pc => pc.Id == pcc.ParallelCorpusId);
                if (corpus.SourceCorpora.Count == 0 && corpus.TargetCorpora.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"The corpus {pcc.ParallelCorpusId} does not have source or target corpora associated with it."
                    );
                }
                if (
                    pcc.SourceFilters != null
                    && pcc.SourceFilters.Count > 0
                    && (
                        pcc.SourceFilters.Select(sf => sf.CorpusId).Distinct().Count() > 1
                        || pcc.SourceFilters[0].CorpusId
                            != engine.ParallelCorpora.Single(pc => pc.Id == pcc.ParallelCorpusId).SourceCorpora[0].Id
                    )
                )
                {
                    throw new InvalidOperationException(
                        $"Only the first source corpus in a parallel corpus may be filtered for pretranslation."
                    );
                }
                pretranslateCorpora.Add(
                    new PretranslateCorpus
                    {
                        ParallelCorpusRef = pcc.ParallelCorpusId,
                        SourceFilters = pcc.SourceFilters?.Select(Map).ToList(),
                    }
                );
            }
        }
        return pretranslateCorpora;
    }

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

        if (
            source.Where(p => p.CorpusId != null).Select(p => p.CorpusId).Distinct().Count()
            != source.Count(p => p.CorpusId != null)
        )
        {
            throw new InvalidOperationException($"Each CorpusId may only be specified once.");
        }

        var corpusIds = new HashSet<string>(engine.Corpora.Select(c => c.Id));
        var parallelCorpusIds = new HashSet<string>(engine.ParallelCorpora.Select(c => c.Id));
        var trainOnCorpora = new List<TrainingCorpus>();
        foreach (TrainingCorpusConfigDto tcc in source)
        {
            if (tcc.CorpusId != null)
            {
                if (tcc.ParallelCorpusId != null)
                {
                    throw new InvalidOperationException($"Only one of ParallelCorpusId and CorpusId can be set.");
                }
                if (!corpusIds.Contains(tcc.CorpusId))
                {
                    throw new InvalidOperationException(
                        $"The corpus {tcc.CorpusId} is not valid: This corpus does not exist for engine {engine.Id}."
                    );
                }
                Corpus corpus = engine.Corpora.Single(c => c.Id == tcc.CorpusId);
                if (corpus.SourceFiles.Count == 0 && corpus.TargetFiles.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"The corpus {tcc.CorpusId} is not valid: This corpus does not have any source or target files."
                    );
                }
                if (tcc.TextIds != null && tcc.ScriptureRange != null)
                {
                    throw new InvalidOperationException(
                        $"The corpus {tcc.CorpusId} is not valid: Set at most one of TextIds and ScriptureRange."
                    );
                }
                trainOnCorpora.Add(
                    new TrainingCorpus
                    {
                        CorpusRef = tcc.CorpusId,
                        TextIds = tcc.TextIds?.ToList(),
                        ScriptureRange = tcc.ScriptureRange,
                    }
                );
            }
            else
            {
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

public partial class TranslationEnginesController
{
    /// <summary>
    /// Starts a build job for a translation engine.
    /// </summary>
    /// <remarks>
    /// Specify the corpora and text ids/scripture ranges within those corpora to train on. Only one type of corpus may be used: either (legacy) corpora (see /translation/engines/{id}/corpora) or parallel corpora (see /translation/engines/{id}/parallel-corpora).
    /// Specifying a corpus:
    /// * A (legacy) corpus is selected by specifying `corpusId` and a parallel corpus is selected by specifying `parallelCorpusId`.
    /// * A parallel corpus can be further filtered by specifying particular corpusIds in `sourceFilters` or `targetFilters`.
    ///
    /// Filtering by text id or chapter:
    /// * Paratext projects can be filtered by [book using the `textIds`](https://github.com/sillsdev/libpalaso/blob/master/SIL.Scripture/Canon.cs).
    /// * Filters can also be supplied via the `scriptureRange` parameter as ranges of biblical text. See [here](https://github.com/sillsdev/serval/wiki/Filtering-Paratext-Project-Data-with-a-Scripture-Range).
    /// * All Paratext project filtering follows original versification. See [here](https://github.com/sillsdev/serval/wiki/Versification-in-Serval) for more information.
    ///
    /// Filter - train on all or none
    /// * If `trainOn` or `pretranslate` is not provided, all corpora will be used for training or pretranslation respectively
    /// * If a corpus is selected for training or pretranslation and neither `scriptureRange` nor `textIds` is defined, all of the selected corpus will be used.
    /// * If a corpus is selected for training or pretranslation and an empty `scriptureRange` or `textIds` is defined, none of the selected corpus will be used.
    /// * If a corpus is selected for training or pretranslation but no further filters are provided, all selected corpora will be used for training or pretranslation respectively.
    ///
    /// Specify the corpora and text ids/scripture ranges within those corpora to pretranslate. When a corpus is selected for pretranslation,
    /// the following text will be pretranslated:
    /// * Text segments that are in the source but do not exist in the target.
    /// * Text segments that are in the source and the target, but because of `trainOn` filtering, have not been trained on.
    /// If the engine does not support pretranslation, these fields have no effect.
    /// Pretranslating uses the same filtering as training.
    ///
    /// The `options` parameter of the build config provides the ability to pass build configuration parameters as a JSON object.
    /// See [nmt job settings documentation](https://github.com/sillsdev/serval/wiki/NMT-Build-Options) about configuring job parameters.
    /// See [smt-transfer job settings documentation](https://github.com/sillsdev/serval/wiki/SMT-Transfer-Build-Options) about configuring job parameters.
    /// See [keyterms parsing documentation](https://github.com/sillsdev/serval/wiki/Paratext-Key-Terms-Parsing) on how to use keyterms for training.
    ///
    /// Note that when using a parallel corpus:
    /// * If, within a single parallel corpus, multiple source corpora have data for the same text ids (for text files or Paratext Projects) or books (for Paratext Projects only using the scripture range), those sources will be mixed where they overlap by randomly choosing from each source per line/verse.
    /// * If, within a single parallel corpus, multiple target corpora have data for the same text ids (for text files or Paratext Projects) or books (for Paratext Projects only using the scripture range), only the first of the targets that includes that text id/book will be used for that text id/book.
    /// </remarks>
    /// <param name="id">The translation engine id</param>
    /// <param name="buildConfig">The build config (see remarks)</param>
    /// <param name="cancellationToken"></param>
    /// <response code="201">The new build job</response>
    /// <response code="400">The build configuration was invalid.</response>
    /// <response code="401">The client is not authenticated.</response>
    /// <response code="403">The authenticated client does not own the translation engine.</response>
    /// <response code="404">The engine does not exist.</response>
    /// <response code="409">There is already an active/pending build or a build in the process of being canceled.</response>
    /// <response code="503">A necessary service is currently unavailable. Check `/health` for more details.</response>
    [Authorize(Scopes.UpdateTranslationEngines)]
    [HttpPost("{id}/builds")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(void), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(void), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<TranslationBuildDto>> StartBuildAsync(
        [NotNull] string id,
        [FromBody] TranslationBuildConfigDto buildConfig,
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
