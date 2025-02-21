namespace Serval.Machine.Shared.Services;

public abstract class PreprocessBuildJob<TEngine> : HangfireBuildJob<TEngine, IReadOnlyList<ParallelCorpus>>
    where TEngine : ITrainingEngine
{
    protected static readonly JsonWriterOptions InferenceWriterOptions = new() { Indented = true };

    internal BuildJobRunnerType TrainJobRunnerType { get; init; } = BuildJobRunnerType.ClearML;

    protected readonly ISharedFileService SharedFileService;
    protected readonly IParallelCorpusPreprocessingService ParallelCorpusPreprocessingService;
    private int _seed = 1234;
    private Random _random;

    public PreprocessBuildJob(
        IPlatformService platformService,
        IRepository<TEngine> engines,
        IDataAccessContext dataAccessContext,
        ILogger<PreprocessBuildJob<TEngine>> logger,
        IBuildJobService<TEngine> buildJobService,
        ISharedFileService sharedFileService,
        IParallelCorpusPreprocessingService parallelCorpusPreprocessingService
    )
        : base(platformService, engines, dataAccessContext, buildJobService, logger)
    {
        SharedFileService = sharedFileService;
        this.ParallelCorpusPreprocessingService = parallelCorpusPreprocessingService;
        _random = new Random(_seed);
    }

    internal int Seed
    {
        get => _seed;
        set
        {
            if (_seed != value)
            {
                _seed = value;
                _random = new Random(_seed);
            }
        }
    }

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
