using Serval.Translation.Engine.V1;
using Corpus = Serval.Translation.Entities.Corpus;
using CorpusFile = Serval.Translation.Entities.CorpusFile;

namespace Serval.Translation.Services;

public class TranslationEngineService : EntityServiceBase<TranslationEngine>, ITranslationEngineService
{
    private readonly IRepository<Build> _builds;
    private readonly IRepository<Corpus> _corpora;
    private readonly GrpcClientFactory _grpcClientFactory;
    private readonly IOptionsMonitor<DataFileOptions> _dataFileOptions;

    public TranslationEngineService(
        IRepository<TranslationEngine> translationEngines,
        IRepository<Build> builds,
        IRepository<Corpus> corpora,
        GrpcClientFactory grpcClientFactory,
        IOptionsMonitor<DataFileOptions> dataFileOptions
    )
        : base(translationEngines)
    {
        _builds = builds;
        _corpora = corpora;
        _grpcClientFactory = grpcClientFactory;
        _dataFileOptions = dataFileOptions;
    }

    public async Task<TranslationResult?> TranslateAsync(string engineId, string segment)
    {
        TranslationEngine? engine = await GetAsync(engineId);
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
            }
        );
        return response.Results[0];
    }

    public async Task<IEnumerable<TranslationResult>?> TranslateAsync(string engineId, int n, string segment)
    {
        TranslationEngine? engine = await GetAsync(engineId);
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
            }
        );
        return response.Results;
    }

    public async Task<WordGraph?> GetWordGraphAsync(string engineId, string segment)
    {
        TranslationEngine? engine = await GetAsync(engineId);
        if (engine == null)
            return null;

        var client = _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engine.Type);
        GetWordGraphResponse response = await client.GetWordGraphAsync(
            new GetWordGraphRequest
            {
                EngineType = engine.Type,
                EngineId = engine.Id,
                Segment = segment
            }
        );
        return response.WordGraph;
    }

    public async Task<bool> TrainSegmentPairAsync(
        string engineId,
        string sourceSegment,
        string targetSegment,
        bool sentenceStart
    )
    {
        TranslationEngine? engine = await GetAsync(engineId);
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
            }
        );
        return true;
    }

    public async Task<IEnumerable<TranslationEngine>> GetAllAsync(string owner)
    {
        return await Entities.GetAllAsync(e => e.Owner == owner);
    }

    public override async Task CreateAsync(TranslationEngine engine)
    {
        var client = _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engine.Type);
        await client.CreateAsync(
            new CreateRequest
            {
                EngineType = engine.Type,
                EngineId = engine.Id,
                SourceLanguageTag = engine.SourceLanguageTag,
                TargetLanguageTag = engine.TargetLanguageTag
            }
        );

        await Entities.InsertAsync(engine);
    }

    public override async Task<bool> DeleteAsync(string engineId)
    {
        TranslationEngine? engine = await Entities.GetAsync(engineId);
        if (engine == null)
            return false;

        var client = _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engine.Type);
        await client.DeleteAsync(new DeleteRequest { EngineType = engine.Type, EngineId = engine.Id });

        await Entities.DeleteAsync(engineId);
        await _builds.DeleteAllAsync(b => b.EngineRef == engineId);

        return true;
    }

    public async Task<Build?> StartBuildAsync(string engineId, CancellationToken cancellationToken = default)
    {
        TranslationEngine? engine = await GetAsync(engineId);
        if (engine == null)
            return null;

        var client = _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engine.Type);
        var request = new StartBuildRequest { EngineType = engine.Type, EngineId = engine.Id };
        foreach (TranslationEngineCorpus corpus in engine.Corpora)
        {
            request.Corpora.Add(
                new ParallelCorpus
                {
                    Pretranslate = corpus.Pretranslate,
                    SourceCorpus = await CreateTextCorpusAsync(
                        corpus.CorpusRef,
                        engine.SourceLanguageTag,
                        cancellationToken
                    ),
                    TargetCorpus = await CreateTextCorpusAsync(
                        corpus.CorpusRef,
                        engine.TargetLanguageTag,
                        cancellationToken
                    )
                }
            );
        }
        StartBuildResponse response = await client.StartBuildAsync(request);

        var build = new Build { EngineRef = engine.Id, BuildId = response.BuildId };
        await _builds.InsertAsync(build);

        return build;
    }

    public async Task CancelBuildAsync(string engineId)
    {
        TranslationEngine? engine = await GetAsync(engineId);
        if (engine == null)
            return;

        var client = _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(engine.Type);
        await client.CancelBuildAsync(new CancelBuildRequest { EngineType = engine.Type, EngineId = engine.Id });
    }

    public Task AddCorpusAsync(string engineId, TranslationEngineCorpus corpus)
    {
        return Entities.UpdateAsync(engineId, u => u.Add(e => e.Corpora, corpus));
    }

    public async Task<bool> DeleteCorpusAsync(string engineId, string corpusId)
    {
        TranslationEngine? engine = await Entities.UpdateAsync(
            engineId,
            u => u.RemoveAll(e => e.Corpora, c => c.CorpusRef == corpusId)
        );
        return engine is not null;
    }

    private async Task<Engine.V1.Corpus?> CreateTextCorpusAsync(
        string corpusId,
        string languageTag,
        CancellationToken cancellationToken
    )
    {
        Corpus? corpus = await _corpora.GetAsync(corpusId, cancellationToken);
        if (corpus is null || corpus.Type != Core.CorpusType.Text)
            return null;
        CorpusFile[] files = corpus.Files.Where(f => f.LanguageTag == languageTag).ToArray();
        if (files.Length == 0)
            return null;

        return new Engine.V1.Corpus
        {
            Format = (Engine.V1.FileFormat)corpus.Format,
            Type = (Engine.V1.CorpusType)corpus.Type,
            Files =
            {
                files.Select(
                    f =>
                        new Engine.V1.CorpusFile
                        {
                            TextId = f.TextId,
                            Filename = GetDataFilePath(f.DataFileRef),
                            LanguageTag = f.LanguageTag
                        }
                )
            }
        };
    }

    private string GetDataFilePath(string id)
    {
        return Path.Combine(_dataFileOptions.CurrentValue.FilesDirectory, id);
    }
}
