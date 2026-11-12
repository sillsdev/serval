namespace Serval.Machine.Translation.Services;

public class LlmRemoteTranslationEngineService(
    [FromKeyedServices(EngineGroup.Translation)] IPlatformService platformService,
    IRepository<TranslationEngine> engines,
    IBuildJobService<TranslationEngine> buildJobService
) : ITranslationEngineService
{
    public async Task CreateAsync(
        string engineId,
        string sourceLanguage,
        string targetLanguage,
        string? engineName = null,
        bool? isModelPersisted = null,
        CancellationToken cancellationToken = default
    )
    {
        ValidateConfiguration(sourceLanguage, targetLanguage);
        try
        {
            var translationEngine = new TranslationEngine
            {
                EngineId = engineId,
                SourceLanguage = sourceLanguage,
                TargetLanguage = targetLanguage,
                Type = EngineType.LlmRemote,
                IsModelPersisted = false, // Models are not persisted
            };
            await engines.InsertAsync(translationEngine, cancellationToken);
        }
        catch (DuplicateKeyException)
        {
            // this method is idempotent, so ignore if the engine already exists
        }
    }

    public async Task DeleteAsync(string engineId, CancellationToken cancellationToken = default)
    {
        await CancelBuildJobAsync(engineId, cancellationToken);
        await engines.DeleteAsync(e => e.EngineId == engineId, cancellationToken);
        await buildJobService.DeleteEngineAsync(engineId, cancellationToken);
    }

    public async Task UpdateAsync(
        string engineId,
        string? sourceLanguage,
        string? targetLanguage,
        CancellationToken cancellationToken = default
    )
    {
        if (sourceLanguage is not null && targetLanguage is not null)
            ValidateConfiguration(sourceLanguage, targetLanguage);

        await CancelBuildJobAsync(engineId, cancellationToken);

        await engines.UpdateAsync(
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
        bool building = !await buildJobService.StartBuildJobAsync(
            BuildJobRunnerType.Local,
            EngineType.LlmRemote,
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

    public Task<string?> CancelBuildAsync(string engineId, CancellationToken cancellationToken = default) =>
        CancelBuildJobAsync(engineId, cancellationToken);

    public Task<ModelDownloadUrlContract> GetModelDownloadUrlAsync(
        string engineId,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException();

    public Task<WordGraphContract> GetWordGraphAsync(
        string engineId,
        string segment,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException();

    public Task<IReadOnlyList<TranslationResultContract>> TranslateAsync(
        string engineId,
        int n,
        string segment,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException();

    public Task TrainSegmentPairAsync(
        string engineId,
        string sourceSegment,
        string targetSegment,
        bool sentenceStart,
        CancellationToken cancellationToken = default
    ) => throw new NotSupportedException();

    public Task<int> GetQueueSizeAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

    public Task<LanguageInfoContract> GetLanguageInfoAsync(
        string language,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(new LanguageInfoContract { InternalCode = language, IsNative = true });

    public void ValidateConfiguration(string sourceLanguage, string targetLanguage) { }

    private async Task<string?> CancelBuildJobAsync(string engineId, CancellationToken cancellationToken)
    {
        (string? buildId, BuildJobState jobState) = await buildJobService.CancelBuildJobAsync(
            engineId,
            cancellationToken
        );
        if (buildId is not null && jobState is BuildJobState.None)
            await platformService.BuildCanceledAsync(buildId, CancellationToken.None);
        return buildId;
    }
}
