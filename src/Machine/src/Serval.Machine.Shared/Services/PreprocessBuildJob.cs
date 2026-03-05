namespace Serval.Machine.Shared.Services;

public abstract class PreprocessBuildJob<TEngine>(
    IPlatformService platformService,
    IRepository<TEngine> engines,
    IDataAccessContext dataAccessContext,
    ILogger<PreprocessBuildJob<TEngine>> logger,
    IBuildJobService<TEngine> buildJobService,
    ISharedFileService sharedFileService,
    IParallelCorpusService parallelCorpusService,
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
    // Using JavaScriptEncoder.Create(UnicodeRanges.All) to avoid escaping surrogate pairs
    // (including those outside of the BMP) which can result in invalid UTF-8.
    // This is safe since the data written by this writer is only read internally and only as UTF-8 encoded JSON.
    protected static readonly JsonWriterOptions InferenceWriterOptions = new()
    {
        Indented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
    };

    internal BuildJobRunnerType TrainJobRunnerType { get; init; } = BuildJobRunnerType.ClearML;
    protected readonly BuildJobOptions BuildJobOptions = options.CurrentValue;
    protected readonly ISharedFileService SharedFileService = sharedFileService;
    protected readonly IParallelCorpusService ParallelCorpusService = parallelCorpusService;

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

        CorpusBundle corpusBundle = new(data);

        (int trainCount, int inferenceCount) = await WriteDataFilesAsync(
            buildId,
            corpusBundle,
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
            corpusBundle,
            cancellationToken
        );

        await UpdateTargetQuoteConventionAsync(engineId, buildId, corpusBundle, cancellationToken);

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
        CorpusBundle corpusBundle,
        CancellationToken cancellationToken
    );

    protected virtual Task UpdateTargetQuoteConventionAsync(
        string engineId,
        string buildId,
        CorpusBundle corpusBundle,
        CancellationToken cancellationToken
    ) => Task.CompletedTask;

    protected abstract Task<(int TrainCount, int InferenceCount)> WriteDataFilesAsync(
        string buildId,
        CorpusBundle corpusBundle,
        string? buildOptions,
        CancellationToken cancellationToken
    );

    protected override async Task CleanupAsync(
        string engineId,
        string buildId,
        IReadOnlyList<ParallelCorpus> parallelCorpora,
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
        CorpusBundle corpusBundle
    )
    {
        List<string> warnings = [];

        foreach (
            (
                string parallelCorpusId,
                string monolingualCorpusId,
                IReadOnlyList<UsfmVersificationError> errors
            ) in ParallelCorpusService.AnalyzeUsfmVersification(corpusBundle)
        )
        {
            foreach (UsfmVersificationError error in errors)
            {
                warnings.Add(
                    error.Type switch
                    {
                        UsfmVersificationErrorType.InvalidChapterNumber =>
                            $"Invalid chapter number error in project {error.ProjectName} at “{error.ActualVerseRef}” (parallel corpus {parallelCorpusId}, monolingual corpus {monolingualCorpusId})",
                        UsfmVersificationErrorType.InvalidVerseNumber =>
                            $"Invalid verse number error in project {error.ProjectName} at “{error.ActualVerseRef}” (parallel corpus {parallelCorpusId}, monolingual corpus {monolingualCorpusId})",
                        _ =>
                            $"USFM versification error in project {error.ProjectName}, expected verse “{error.ExpectedVerseRef}”, actual verse “{error.ActualVerseRef}”, mismatch type {error.Type} (parallel corpus {parallelCorpusId}, monolingual corpus {monolingualCorpusId})",
                    }
                );
            }
        }

        foreach (
            (
                string parallelCorpusId,
                string monolingualCorpusId,
                MissingParentProjectError error
            ) in ParallelCorpusService.FindMissingParentProjects(corpusBundle)
        )
        {
            warnings.Add(
                $"Unable to locate parent project {error.ParentProjectName} of daughter project {error.ProjectName} (parallel corpus {parallelCorpusId}, monolingual corpus {monolingualCorpusId})"
            );
        }

        return warnings;
    }
}
