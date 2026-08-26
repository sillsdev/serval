namespace Serval.Machine.Translation.Services;

public class NmtEngineService(
    ITranslationPlatformService platformService,
    IRepository<TranslationEngine> engines,
    IBuildJobService<TranslationEngine> buildJobService,
    ILanguageTagService languageTagService,
    IClearMLQueueService<TranslationEngine> clearMLQueueService,
    ISharedFileService sharedFileService
) : ITranslationEngineService
{
    private readonly ITranslationPlatformService _platformService = platformService;
    private readonly IRepository<TranslationEngine> _engines = engines;
    private readonly IBuildJobService<TranslationEngine> _buildJobService = buildJobService;
    private readonly IClearMLQueueService<TranslationEngine> _clearMLQueueService = clearMLQueueService;
    private readonly ILanguageTagService _languageTagService = languageTagService;
    private readonly ISharedFileService _sharedFileService = sharedFileService;
    public const string ModelDirectory = "models/";

    private static readonly IReadOnlyDictionary<string, string> ModelToFullModelName = new Dictionary<string, string>()
    {
        [Models.Models.Nllb] = "facebook/nllb-200-distilled-1.3B",
        [Models.Models.Nllb600m] = "facebook/nllb-200-distilled-600M",
        [Models.Models.NllbTesting] = "hf-internal-testing/tiny-random-nllb",
    };

    private static readonly IReadOnlyDictionary<string, string> FullModelNameToModel = new Dictionary<string, string>()
    {
        ["facebook/nllb-200-distilled-1.3B"] = Models.Models.Nllb,
        ["facebook/nllb-200-distilled-600M"] = Models.Models.Nllb600m,
        ["hf-internal-testing/tiny-random-nllb"] = Models.Models.NllbTesting,
    };

    public static string GetModelPath(string engineId, int buildRevision)
    {
        return $"{ModelDirectory}{engineId}_{buildRevision}.tar.gz";
    }

    private const int MinutesToExpire = 60;

    public async Task CreateAsync(
        string engineId,
        string sourceLanguage,
        string targetLanguage,
        string? engineName = null,
        bool? isModelPersisted = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var translationEngine = new TranslationEngine
            {
                EngineId = engineId,
                SourceLanguage = sourceLanguage,
                TargetLanguage = targetLanguage,
                Type = EngineType.Nmt,
                IsModelPersisted = isModelPersisted ?? false, // models are not persisted if not specified
            };
            await _engines.InsertAsync(translationEngine, cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            // this method is idempotent, so ignore if the engine already exists
        }
    }

    public async Task DeleteAsync(string engineId, CancellationToken cancellationToken = default)
    {
        await CancelBuildJobAsync(engineId, cancellationToken);
        await _engines.DeleteAsync(e => e.EngineId == engineId, cancellationToken);
        await _buildJobService.DeleteEngineAsync(engineId, cancellationToken);
    }

    public async Task UpdateAsync(
        string engineId,
        string? sourceLanguage,
        string? targetLanguage,
        CancellationToken cancellationToken = default
    )
    {
        await CancelBuildJobAsync(engineId, cancellationToken);

        await _engines.UpdateAsync(
            e => e.EngineId == engineId,
            u =>
            {
                if (sourceLanguage is not null)
                    u.Set(e => e.SourceLanguage, sourceLanguage);
                if (targetLanguage is not null)
                    u.Set(e => e.TargetLanguage, targetLanguage);
            },
            cancellationToken: cancellationToken
        );
    }

    public async Task<StartBuildContract> StartBuildAsync(
        string engineId,
        string buildId,
        IReadOnlyList<ParallelCorpusContract> corpora,
        string? options = null,
        string? model = null,
        CancellationToken cancellationToken = default
    )
    {
        JsonObject? buildOptionsJsonObject = [];
        if (options != null)
        {
            try
            {
                JsonNode? buildOptionsJsonNode = JsonNode.Parse(options);
                if (buildOptionsJsonNode is JsonObject obj)
                    buildOptionsJsonObject = obj;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Unable to parse field build options : {e.Message}", e);
            }
        }

        if (
            buildOptionsJsonObject.ContainsKey("parent_model_name")
            && buildOptionsJsonObject["parent_model_name"] != null
            && model == null
        )
        {
            model = GetModelName(buildOptionsJsonObject["parent_model_name"]!.GetValue<string>());
        }
        else
        {
            model ??= Models.Models.Nllb;
            buildOptionsJsonObject["parent_model_name"] = GetFullModelName(model);
            options = buildOptionsJsonObject.ToJsonString();
        }

        bool building = !await _buildJobService.StartBuildJobAsync(
            BuildJobRunnerType.Local,
            EngineType.Nmt,
            engineId,
            buildId,
            BuildStage.Preprocess,
            corpora,
            options,
            model,
            cancellationToken
        );
        // If there is a pending/running build, then no need to start a new one.
        if (building)
            throw new ConflictException();

        return new() { Model = model };
    }

    public Task<string?> CancelBuildAsync(string engineId, CancellationToken cancellationToken = default)
    {
        return CancelBuildJobAsync(engineId, cancellationToken);
    }

    public async Task<ModelDownloadUrlContract> GetModelDownloadUrlAsync(
        string engineId,
        CancellationToken cancellationToken = default
    )
    {
        TranslationEngine engine = await GetEngineAsync(engineId, cancellationToken);
        if (engine.IsModelPersisted != true)
        {
            throw new NotSupportedException(
                "The model cannot be downloaded. "
                    + "To enable downloading the model, recreate the engine with IsModelPersisted property to true."
            );
        }

        if (engine.BuildRevision == 0)
            throw new InvalidOperationException("The engine has not been built yet.");
        string filepath = GetModelPath(engineId, engine.BuildRevision);
        bool fileExists = await _sharedFileService.ExistsAsync(filepath, cancellationToken);
        if (!fileExists)
            throw new FileNotFoundException($"The model for build revision , {engine.BuildRevision}, does not exist.");
        DateTime expiresAt = DateTime.UtcNow.AddMinutes(MinutesToExpire);
        var modelInfo = new ModelDownloadUrlContract
        {
            Url = await _sharedFileService.GetDownloadUrlAsync(filepath, expiresAt),
            ModelRevision = engine.BuildRevision,
            ExpiresAt = expiresAt,
        };
        return modelInfo;
    }

    public Task<IReadOnlyList<TranslationResultContract>> TranslateAsync(
        string engineId,
        int n,
        string segment,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotSupportedException();
    }

    public Task<WordGraphContract> GetWordGraphAsync(
        string engineId,
        string segment,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotSupportedException();
    }

    public Task TrainSegmentPairAsync(
        string engineId,
        string sourceSegment,
        string targetSegment,
        bool sentenceStart,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotSupportedException();
    }

    public Task<int> GetQueueSizeAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_clearMLQueueService.GetQueueSize(EngineType.Nmt));
    }

    public Task<LanguageInfoContract> GetLanguageInfoAsync(
        string language,
        CancellationToken cancellationToken = default
    )
    {
        bool isNative = IsLanguageNativeToModel(language, out string internalCode);
        return Task.FromResult(new LanguageInfoContract { IsNative = isNative, InternalCode = internalCode });
    }

    private bool IsLanguageNativeToModel(string language, out string internalCode)
    {
        return _languageTagService.ConvertToFlores200Code(language, out internalCode)
            == Flores200Support.LanguageAndScript;
    }

    private async Task<string?> CancelBuildJobAsync(string engineId, CancellationToken cancellationToken)
    {
        (string? buildId, BuildJobState jobState) = await _buildJobService.CancelBuildJobAsync(
            engineId,
            cancellationToken
        );
        if (buildId is not null && jobState is BuildJobState.None)
            await _platformService.BuildCanceledAsync(buildId, CancellationToken.None);
        return buildId;
    }

    private async Task<TranslationEngine> GetEngineAsync(string engineId, CancellationToken cancellationToken)
    {
        TranslationEngine? engine = await _engines.GetAsync(e => e.EngineId == engineId, cancellationToken);
        if (engine is null)
            throw new EngineNotFoundException($"The engine {engineId} does not exist.");
        return engine;
    }

    private static string GetFullModelName(string model)
    {
        if (ModelToFullModelName.TryGetValue(model, out string? fullModelName) && fullModelName != null)
            return fullModelName;
        throw new InvalidOperationException($"Unknown model {model}.");
    }

    private static string GetModelName(string fullModelName)
    {
        if (FullModelNameToModel.TryGetValue(fullModelName, out string? model) && model != null)
            return model;
        throw new InvalidOperationException($"Unknown full model name {fullModelName}.");
    }
}
