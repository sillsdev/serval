﻿namespace Serval.Machine.Shared.Services;

public class NmtEngineService(
    [FromKeyedServices(EngineGroup.Translation)] IPlatformService platformService,
    IDataAccessContext dataAccessContext,
    IRepository<TranslationEngine> engines,
    IBuildJobService<TranslationEngine> buildJobService,
    ILanguageTagService languageTagService,
    IClearMLQueueService clearMLQueueService,
    ISharedFileService sharedFileService
) : ITranslationEngineService
{
    private readonly IPlatformService _platformService = platformService;
    private readonly IDataAccessContext _dataAccessContext = dataAccessContext;
    private readonly IRepository<TranslationEngine> _engines = engines;
    private readonly IBuildJobService<TranslationEngine> _buildJobService = buildJobService;
    private readonly IClearMLQueueService _clearMLQueueService = clearMLQueueService;
    private readonly ILanguageTagService _languageTagService = languageTagService;
    private readonly ISharedFileService _sharedFileService = sharedFileService;
    public const string ModelDirectory = "models/";

    public static string GetModelPath(string engineId, int buildRevision)
    {
        return $"{ModelDirectory}{engineId}_{buildRevision}.tar.gz";
    }

    public EngineType Type => EngineType.Nmt;

    private const int MinutesToExpire = 60;

    public async Task<TranslationEngine> CreateAsync(
        string engineId,
        string? engineName,
        string sourceLanguage,
        string targetLanguage,
        bool? isModelPersisted = null,
        CancellationToken cancellationToken = default
    )
    {
        var translationEngine = await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                var translationEngine = new TranslationEngine
                {
                    EngineId = engineId,
                    SourceLanguage = sourceLanguage,
                    TargetLanguage = targetLanguage,
                    Type = EngineType.Nmt,
                    IsModelPersisted = isModelPersisted ?? false // models are not persisted if not specified
                };
                await _engines.InsertAsync(translationEngine, ct);
                await _buildJobService.CreateEngineAsync(engineId, engineName, ct);
                return translationEngine;
            },
            cancellationToken: cancellationToken
        );
        return translationEngine;
    }

    public async Task DeleteAsync(string engineId, CancellationToken cancellationToken = default)
    {
        await CancelBuildJobAsync(engineId, cancellationToken);

        await _engines.DeleteAsync(e => e.EngineId == engineId, cancellationToken);
        await _buildJobService.DeleteEngineAsync(engineId, CancellationToken.None);
    }

    public async Task UpdateAsync(
        string engineId,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default
    )
    {
        await CancelBuildJobAsync(engineId, cancellationToken);

        await _engines.UpdateAsync(
            e => e.EngineId == engineId,
            u =>
            {
                u.Set(e => e.SourceLanguage, sourceLanguage);
                u.Set(e => e.TargetLanguage, targetLanguage);
            },
            cancellationToken: cancellationToken
        );
    }

    public async Task StartBuildAsync(
        string engineId,
        string buildId,
        string? buildOptions,
        IReadOnlyList<ParallelCorpus> corpora,
        CancellationToken cancellationToken = default
    )
    {
        bool building = !await _buildJobService.StartBuildJobAsync(
            BuildJobRunnerType.Hangfire,
            EngineType.Nmt,
            engineId,
            buildId,
            BuildStage.Preprocess,
            corpora,
            buildOptions,
            cancellationToken
        );
        // If there is a pending/running build, then no need to start a new one.
        if (building)
            throw new InvalidOperationException("The engine is already building or in the process of canceling.");
    }

    public async Task<string> CancelBuildAsync(string engineId, CancellationToken cancellationToken = default)
    {
        string? buildId = await CancelBuildJobAsync(engineId, cancellationToken);
        if (buildId is null)
            throw new InvalidOperationException("The engine is not currently building.");
        return buildId;
    }

    public async Task<ModelDownloadUrl> GetModelDownloadUrlAsync(
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
        var expiresAt = DateTime.UtcNow.AddMinutes(MinutesToExpire);
        var modelInfo = new ModelDownloadUrl
        {
            Url = await _sharedFileService.GetDownloadUrlAsync(filepath, expiresAt),
            ModelRevision = engine.BuildRevision,
            ExpiresAt = expiresAt
        };
        return modelInfo;
    }

    public Task<IReadOnlyList<TranslationResult>> TranslateAsync(
        string engineId,
        int n,
        string segment,
        CancellationToken cancellationToken = default
    )
    {
        throw new NotSupportedException();
    }

    public Task<WordGraph> GetWordGraphAsync(
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

    public int GetQueueSize()
    {
        return _clearMLQueueService.GetQueueSize(Type);
    }

    public bool IsLanguageNativeToModel(string language, out string internalCode)
    {
        return _languageTagService.ConvertToFlores200Code(language, out internalCode);
    }

    private async Task<string?> CancelBuildJobAsync(string engineId, CancellationToken cancellationToken)
    {
        string? buildId = null;
        await _dataAccessContext.WithTransactionAsync(
            async (ct) =>
            {
                (buildId, BuildJobState jobState) = await _buildJobService.CancelBuildJobAsync(engineId, ct);
                if (buildId is not null && jobState is BuildJobState.None)
                    await _platformService.BuildCanceledAsync(buildId, CancellationToken.None);
            },
            cancellationToken: cancellationToken
        );
        return buildId;
    }

    private async Task<TranslationEngine> GetEngineAsync(string engineId, CancellationToken cancellationToken)
    {
        TranslationEngine? engine = await _engines.GetAsync(e => e.EngineId == engineId, cancellationToken);
        if (engine is null)
            throw new InvalidOperationException($"The engine {engineId} does not exist.");
        return engine;
    }
}
