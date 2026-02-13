namespace Serval.Machine.Shared.Services;

public abstract class PreprocessBuildJob<TEngine>(
    IPlatformService platformService,
    IRepository<TEngine> engines,
    IDataAccessContext dataAccessContext,
    ILogger<PreprocessBuildJob<TEngine>> logger,
    IBuildJobService<TEngine> buildJobService,
    ISharedFileService sharedFileService,
    IParallelCorpusPreprocessingService parallelCorpusPreprocessingService,
    IOptionsMonitor<BuildJobOptions> options
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
    // Using UnsafeRelaxedJsonEscaping to avoid escaping surrogate pairs which can result in invalid UTF-8.
    // This is safe since the data written by this writer is only read internally and only as UTF-8 encoded JSON.
    protected static readonly JsonWriterOptions InferenceWriterOptions = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    internal BuildJobRunnerType TrainJobRunnerType { get; init; } = BuildJobRunnerType.ClearML;
    protected readonly BuildJobOptions BuildJobOptions = options.CurrentValue;
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
            engine.SourceLanguage,
            engine.TargetLanguage,
            data,
            cancellationToken
        );

        await UpdateTargetQuoteConventionAsync(engineId, buildId, data, cancellationToken);

        if (inferenceCount == 0 && engine is TranslationEngine { IsModelPersisted: false })
        {
            throw new InvalidOperationException(
                $"There was no data specified for inferencing in build {buildId}. Build canceled."
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
        string sourceLanguageTag,
        string targetLanguageTag,
        IReadOnlyList<ParallelCorpus> corpora,
        CancellationToken cancellationToken
    );

    protected virtual Task UpdateTargetQuoteConventionAsync(
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

    protected virtual IReadOnlyList<string> GetWarnings(
        int trainCount,
        int inferenceCount,
        string sourceLanguageTag,
        string targetLanguageTag,
        IReadOnlyList<ParallelCorpus> corpora
    )
    {
        List<string> warnings = [];

        foreach (ParallelCorpus parallelCorpus in corpora)
        {
            IReadOnlyList<(string MonolingualCorpusId, IReadOnlyList<UsfmVersificationError> errors)> errorsPerCorpus =
                ParallelCorpusPreprocessingService.AnalyzeUsfmVersification(parallelCorpus);

            foreach ((string monolingualCorpusId, IReadOnlyList<UsfmVersificationError> errors) in errorsPerCorpus)
            {
                foreach (UsfmVersificationError error in errors)
                {
                    warnings.Add(
                        error.Type switch
                        {
                            UsfmVersificationErrorType.InvalidChapterNumber =>
                                $"Invalid chapter number error in project {error.ProjectName} at “{error.ActualVerseRef}” (parallel corpus {parallelCorpus.Id}, monolingual corpus {monolingualCorpusId})",
                            UsfmVersificationErrorType.InvalidVerseNumber =>
                                $"Invalid verse number error in project {error.ProjectName} at “{error.ActualVerseRef}” (parallel corpus {parallelCorpus.Id}, monolingual corpus {monolingualCorpusId})",
                            _ =>
                                $"USFM versification error in project {error.ProjectName}, expected verse “{error.ExpectedVerseRef}”, actual verse “{error.ActualVerseRef}”, mismatch type {error.Type} (parallel corpus {parallelCorpus.Id}, monolingual corpus {monolingualCorpusId})",
                        }
                    );
                }
            }
        }
        return warnings;
    }
}
