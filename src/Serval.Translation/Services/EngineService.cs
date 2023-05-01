using Serval.Translation.V1;

namespace Serval.Translation.Services;

public class EngineService : EntityServiceBase<Engine>, IEngineService
{
    private readonly IRepository<Build> _builds;
    private readonly IRepository<Pretranslation> _pretranslations;
    private readonly GrpcClientFactory _grpcClientFactory;
    private readonly IOptionsMonitor<DataFileOptions> _dataFileOptions;
    private readonly IDataAccessContext _dataAccessContext;

    public EngineService(
        IRepository<Engine> engines,
        IRepository<Build> builds,
        IRepository<Pretranslation> pretranslations,
        GrpcClientFactory grpcClientFactory,
        IOptionsMonitor<DataFileOptions> dataFileOptions,
        IDataAccessContext dataAccessContext
    )
        : base(engines)
    {
        _builds = builds;
        _pretranslations = pretranslations;
        _grpcClientFactory = grpcClientFactory;
        _dataFileOptions = dataFileOptions;
        _dataAccessContext = dataAccessContext;
    }

    public async Task<Models.TranslationResult?> TranslateAsync(
        string engineId,
        string segment,
        CancellationToken cancellationToken = default
    )
    {
        Engine? engine = await GetAsync(engineId, cancellationToken);
        if (engine == null)
            return null;

        var client = _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engine.Type);
        TranslateResponse response = await client.TranslateAsync(
            new TranslateRequest
            {
                EngineType = engine.Type,
                EngineId = engine.Id,
                N = 1,
                Segment = segment
            },
            cancellationToken: cancellationToken
        );
        return Map(response.Results[0]);
    }

    public async Task<IEnumerable<Models.TranslationResult>?> TranslateAsync(
        string engineId,
        int n,
        string segment,
        CancellationToken cancellationToken = default
    )
    {
        Engine? engine = await GetAsync(engineId, cancellationToken);
        if (engine == null)
            return null;

        var client = _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engine.Type);
        TranslateResponse response = await client.TranslateAsync(
            new TranslateRequest
            {
                EngineType = engine.Type,
                EngineId = engine.Id,
                N = n,
                Segment = segment
            },
            cancellationToken: cancellationToken
        );
        return response.Results.Select(Map);
    }

    public async Task<Models.WordGraph?> GetWordGraphAsync(
        string engineId,
        string segment,
        CancellationToken cancellationToken = default
    )
    {
        Engine? engine = await GetAsync(engineId, cancellationToken);
        if (engine == null)
            return null;

        var client = _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engine.Type);
        GetWordGraphResponse response = await client.GetWordGraphAsync(
            new GetWordGraphRequest
            {
                EngineType = engine.Type,
                EngineId = engine.Id,
                Segment = segment
            },
            cancellationToken: cancellationToken
        );
        return Map(response.WordGraph);
    }

    public async Task<bool> TrainSegmentPairAsync(
        string engineId,
        string sourceSegment,
        string targetSegment,
        bool sentenceStart,
        CancellationToken cancellationToken = default
    )
    {
        Engine? engine = await GetAsync(engineId, cancellationToken);
        if (engine == null)
            return false;

        var client = _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engine.Type);
        await client.TrainSegmentPairAsync(
            new TrainSegmentPairRequest
            {
                EngineType = engine.Type,
                EngineId = engine.Id,
                SourceSegment = sourceSegment,
                TargetSegment = targetSegment,
                SentenceStart = sentenceStart
            },
            cancellationToken: cancellationToken
        );
        return true;
    }

    public async Task<IEnumerable<Engine>> GetAllAsync(string owner, CancellationToken cancellationToken = default)
    {
        return await Entities.GetAllAsync(e => e.Owner == owner, cancellationToken);
    }

    public override async Task CreateAsync(Engine engine, CancellationToken cancellationToken = default)
    {
        await Entities.InsertAsync(engine, cancellationToken);
        try
        {
            var client = _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engine.Type);
            var request = new CreateRequest
            {
                EngineType = engine.Type,
                EngineId = engine.Id,
                SourceLanguage = engine.SourceLanguage,
                TargetLanguage = engine.TargetLanguage
            };
            if (engine.Name is not null)
                request.EngineName = engine.Name;
            await client.CreateAsync(request, cancellationToken: cancellationToken);
        }
        catch
        {
            await Entities.DeleteAsync(engine, CancellationToken.None);
            throw;
        }
    }

    public override async Task<bool> DeleteAsync(string engineId, CancellationToken cancellationToken = default)
    {
        Engine? engine = await Entities.GetAsync(engineId, cancellationToken);
        if (engine == null)
            return false;

        var client = _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engine.Type);
        await client.DeleteAsync(
            new DeleteRequest { EngineType = engine.Type, EngineId = engine.Id },
            cancellationToken: cancellationToken
        );

        await _dataAccessContext.BeginTransactionAsync(CancellationToken.None);
        await Entities.DeleteAsync(engineId, CancellationToken.None);
        await _builds.DeleteAllAsync(b => b.EngineRef == engineId, CancellationToken.None);
        await _pretranslations.DeleteAllAsync(pt => pt.EngineRef == engineId, CancellationToken.None);
        await _dataAccessContext.CommitTransactionAsync(CancellationToken.None);

        return true;
    }

    public async Task<bool> StartBuildAsync(Build build, CancellationToken cancellationToken = default)
    {
        Engine? engine = await GetAsync(build.EngineRef, cancellationToken);
        if (engine == null)
            return false;

        await _builds.InsertAsync(build, cancellationToken);

        try
        {
            Dictionary<string, PretranslateCorpus>? pretranslate = build.Pretranslate?.ToDictionary(c => c.CorpusRef);
            var client = _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engine.Type);
            var request = new StartBuildRequest
            {
                EngineType = engine.Type,
                EngineId = engine.Id,
                BuildId = build.Id,
                Corpora =
                {
                    engine.Corpora.Select(c =>
                    {
                        V1.Corpus corpus = Map(c);
                        if (pretranslate?.TryGetValue(c.Id, out PretranslateCorpus? pretranslateCorpus) ?? false)
                        {
                            corpus.PretranslateAll =
                                pretranslateCorpus.TextIds is null || pretranslateCorpus.TextIds.Count == 0;
                            if (pretranslateCorpus.TextIds is not null)
                                corpus.PretranslateTextIds.Add(pretranslateCorpus.TextIds);
                        }
                        return corpus;
                    })
                }
            };

            await client.StartBuildAsync(request, cancellationToken: cancellationToken);
        }
        catch
        {
            await _builds.DeleteAsync(build, CancellationToken.None);
            throw;
        }

        return true;
    }

    public async Task CancelBuildAsync(string engineId, CancellationToken cancellationToken = default)
    {
        Engine? engine = await GetAsync(engineId, cancellationToken);
        if (engine == null)
            return;

        var client = _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engine.Type);
        await client.CancelBuildAsync(
            new CancelBuildRequest { EngineType = engine.Type, EngineId = engine.Id },
            cancellationToken: cancellationToken
        );
    }

    public Task AddCorpusAsync(string engineId, Models.Corpus corpus, CancellationToken cancellationToken = default)
    {
        return Entities.UpdateAsync(engineId, u => u.Add(e => e.Corpora, corpus), cancellationToken: cancellationToken);
    }

    public async Task<bool> DeleteCorpusAsync(
        string engineId,
        string corpusId,
        CancellationToken cancellationToken = default
    )
    {
        await _dataAccessContext.BeginTransactionAsync(cancellationToken);
        Engine? engine = await Entities.UpdateAsync(
            engineId,
            u => u.RemoveAll(e => e.Corpora, c => c.Id == corpusId),
            cancellationToken: cancellationToken
        );
        await _pretranslations.DeleteAllAsync(pt => pt.CorpusRef == corpusId, cancellationToken);
        await _dataAccessContext.CommitTransactionAsync(CancellationToken.None);
        return engine is not null;
    }

    public Task DeleteAllCorpusFilesAsync(string dataFileId, CancellationToken cancellationToken = default)
    {
        return Entities.UpdateAllAsync(
            e =>
                e.Corpora.Any(
                    c => c.SourceFiles.Any(f => f.Id == dataFileId) || c.TargetFiles.Any(f => f.Id == dataFileId)
                ),
            u =>
                u.RemoveAll(e => e.Corpora[ArrayPosition.All].SourceFiles, f => f.Id == dataFileId)
                    .RemoveAll(e => e.Corpora[ArrayPosition.All].TargetFiles, f => f.Id == dataFileId),
            cancellationToken
        );
    }

    private Models.TranslationResult Map(V1.TranslationResult source)
    {
        return new Models.TranslationResult
        {
            Translation = source.Translation,
            SourceTokens = source.SourceTokens.ToList(),
            TargetTokens = source.TargetTokens.ToList(),
            Confidences = source.Confidences.ToList(),
            Sources = source.Sources.Select(Map).ToList(),
            Alignment = source.Alignment.Select(Map).ToList(),
            Phrases = source.Phrases.Select(Map).ToList()
        };
    }

    private IReadOnlySet<Contracts.TranslationSource> Map(TranslationSources source)
    {
        return source.Values.Cast<Contracts.TranslationSource>().ToHashSet();
    }

    private Models.AlignedWordPair Map(V1.AlignedWordPair source)
    {
        return new Models.AlignedWordPair { SourceIndex = source.SourceIndex, TargetIndex = source.TargetIndex };
    }

    private Models.Phrase Map(V1.Phrase source)
    {
        return new Models.Phrase
        {
            SourceSegmentStart = source.SourceSegmentStart,
            SourceSegmentEnd = source.SourceSegmentEnd,
            TargetSegmentCut = source.TargetSegmentCut,
            Confidence = source.Confidence
        };
    }

    private Models.WordGraph Map(V1.WordGraph source)
    {
        return new Models.WordGraph
        {
            SourceTokens = source.SourceTokens.ToList(),
            InitialStateScore = source.InitialStateScore,
            FinalStates = source.FinalStates.ToHashSet(),
            Arcs = source.Arcs.Select(Map).ToList()
        };
    }

    private Models.WordGraphArc Map(V1.WordGraphArc source)
    {
        return new Models.WordGraphArc
        {
            PrevState = source.PrevState,
            NextState = source.NextState,
            Score = source.Score,
            TargetTokens = source.TargetTokens.ToList(),
            Confidences = source.Confidences.ToList(),
            SourceSegmentStart = source.SourceSegmentStart,
            SourceSegmentEnd = source.SourceSegmentEnd,
            Alignment = source.Alignment.Select(Map).ToList(),
            Sources = source.Sources.Select(Map).ToList()
        };
    }

    private V1.Corpus Map(Models.Corpus source)
    {
        return new V1.Corpus
        {
            Id = source.Id,
            SourceLanguage = source.SourceLanguage,
            TargetLanguage = source.TargetLanguage,
            SourceFiles = { source.SourceFiles.Select(Map) },
            TargetFiles = { source.TargetFiles.Select(Map) }
        };
    }

    private V1.CorpusFile Map(Models.CorpusFile source)
    {
        return new V1.CorpusFile
        {
            TextId = source.TextId,
            Format = (V1.FileFormat)source.Format,
            Location = Path.Combine(_dataFileOptions.CurrentValue.FilesDirectory, source.Filename)
        };
    }
}
