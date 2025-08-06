namespace Serval.Machine.Shared.Services;

public abstract class PreprocessBuildJob<TEngine>(
    IPlatformService platformService,
    IRepository<TEngine> engines,
    IDataAccessContext dataAccessContext,
    ILogger<PreprocessBuildJob<TEngine>> logger,
    IBuildJobService<TEngine> buildJobService,
    ISharedFileService sharedFileService,
    IParallelCorpusPreprocessingService parallelCorpusPreprocessingService
)
    : HangfireBuildJob<TEngine, IReadOnlyList<ParallelCorpus>>(
        platformService,
        engines,
        dataAccessContext,
        buildJobService,
        logger
    )
    where TEngine : ITrainingEngine
{
    protected static readonly JsonWriterOptions InferenceWriterOptions = new() { Indented = true };

    internal BuildJobRunnerType TrainJobRunnerType { get; init; } = BuildJobRunnerType.ClearML;

    protected readonly ISharedFileService SharedFileService = sharedFileService;
    protected readonly IParallelCorpusPreprocessingService ParallelCorpusPreprocessingService =
        parallelCorpusPreprocessingService;

    protected override async Task DoWorkAsync(
        string engineId,
        string buildId,
        IReadOnlyList<ParallelCorpus> data,
        string? buildOptions,
        CancellationToken cancellationToken
    )
    {
        TEngine? engine = await Engines.GetAsync(e => e.EngineId == engineId, cancellationToken);
        if (engine is null)
            throw new OperationCanceledException($"Engine {engineId} does not exist.  Build canceled.");

        bool sourceTagInBaseModel = ResolveLanguageCodeForBaseModel(engine.SourceLanguage, out string srcLang);
        bool targetTagInBaseModel = ResolveLanguageCodeForBaseModel(engine.TargetLanguage, out string trgLang);

        (int trainCount, int inferenceCount) = await WriteDataFilesAsync(
            buildId,
            data,
            buildOptions,
            cancellationToken
        );

        await UpdateBuildExecutionData(
            engineId,
            buildId,
            trainCount,
            inferenceCount,
            srcLang,
            trgLang,
            cancellationToken
        );

        await UpdateParallelCorpusAnalysisAsync(engineId, buildId, data, cancellationToken);

        if (trainCount == 0 && (!sourceTagInBaseModel || !targetTagInBaseModel))
        {
            throw new OperationCanceledException(
                $"At least one language code in build {buildId} is unknown to the base model, and the data specified for training was empty. Build canceled."
            );
        }

        cancellationToken.ThrowIfCancellationRequested();

        bool canceling = !await BuildJobService.StartBuildJobAsync(
            TrainJobRunnerType,
            engine.Type,
            engineId,
            buildId,
            BuildStage.Train,
            buildOptions: buildOptions,
            cancellationToken: cancellationToken
        );
        if (canceling)
            throw new OperationCanceledException();
    }

    protected abstract Task UpdateBuildExecutionData(
        string engineId,
        string buildId,
        int trainCount,
        int inferenceCount,
        string srcLang,
        string trgLang,
        CancellationToken cancellationToken
    );

    protected virtual Task UpdateParallelCorpusAnalysisAsync(
        string engineId,
        string buildId,
        IReadOnlyList<ParallelCorpus> corpora,
        CancellationToken cancellationToken
    ) => Task.CompletedTask;

    protected abstract Task<(int TrainCount, int InferenceCount)> WriteDataFilesAsync(
        string buildId,
        IReadOnlyList<ParallelCorpus> corpora,
        string? buildOptions,
        CancellationToken cancellationToken
    );

    protected override async Task CleanupAsync(
        string engineId,
        string buildId,
        IReadOnlyList<ParallelCorpus> data,
        JobCompletionStatus completionStatus
    )
    {
        if (completionStatus is JobCompletionStatus.Canceled)
        {
            try
            {
                await SharedFileService.DeleteAsync($"builds/{buildId}/");
            }
            catch (Exception e)
            {
                Logger.LogWarning(e, "Unable to to delete job data for build {BuildId}.", buildId);
            }
        }
    }

    protected virtual bool ResolveLanguageCodeForBaseModel(string languageCode, out string resolvedCode)
    {
        resolvedCode = languageCode;
        return true;
    }
}
