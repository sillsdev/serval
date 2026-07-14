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
    : BuildJob<TEngine, IReadOnlyList<ParallelCorpusContract>>(
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

    internal BuildJobRunnerType TrainJobRunnerType { get; set; } = BuildJobRunnerType.ClearML;
    protected readonly BuildJobOptions BuildJobOptions = options.CurrentValue;
    protected readonly ISharedFileService SharedFileService = sharedFileService;
    protected readonly IParallelCorpusService ParallelCorpusService = parallelCorpusService;

    protected override async Task DoWorkAsync(
        string engineId,
        string buildId,
        IReadOnlyList<ParallelCorpusContract> data,
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
        IReadOnlyList<ParallelCorpusContract> parallelCorpora,
        CancellationToken cancellationToken
    );

    protected virtual Task UpdateTargetQuoteConventionAsync(
        string engineId,
        string buildId,
        IReadOnlyList<ParallelCorpusContract> parallelCorpora,
        CancellationToken cancellationToken
    ) => Task.CompletedTask;

    protected abstract Task<(int TrainCount, int InferenceCount)> WriteDataFilesAsync(
        string buildId,
        IReadOnlyList<ParallelCorpusContract> parallelCorpora,
        string? buildOptions,
        CancellationToken cancellationToken
    );

    protected override async Task CleanupAsync(string engineId, string buildId, JobCompletionStatus completionStatus)
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
        IReadOnlyList<ParallelCorpusContract> parallelCorpora
    )
    {
        List<string> warnings = [];

        foreach (
            (
                string parallelCorpusId,
                string monolingualCorpusId,
                string projectName,
                IReadOnlyList<UsfmVersificationDiagnosticContract> diagnostics
            ) in ParallelCorpusService.AnalyzeUsfmVersification(parallelCorpora)
        )
        {
            foreach (UsfmVersificationDiagnosticContract diagnostic in diagnostics)
            {
                var sb = new StringBuilder();
                if (diagnostic.NumAffectedVerses > 1)
                {
                    sb.Append("for ");
                    sb.Append(diagnostic.NumAffectedVerses);
                    sb.Append(" verses ");
                }

                sb.Append("in project ");
                sb.Append(projectName);
                sb.Append(" at “");
                sb.AppendJoin(", ", diagnostic.References);
                sb.Append("” on ");
                sb.Append(diagnostic.LineNumbers.Count == 1 ? "line" : "lines");
                sb.Append(' ');
                sb.AppendJoin(", ", diagnostic.LineNumbers);
                sb.Append(" of ");
                sb.Append(diagnostic.Filename);
                sb.Append("(parallel corpus ");
                sb.Append(parallelCorpusId);
                sb.Append(", monolingual corpus ");
                sb.Append(monolingualCorpusId);
                sb.Append(')');
                warnings.Add(
                    diagnostic.Type switch
                    {
                        Serval.Shared.Contracts.UsfmVersificationDiagnosticType.Missing => $"Missing content {sb}",
                        Serval.Shared.Contracts.UsfmVersificationDiagnosticType.Extra => $"Extra content {sb}",
                        Serval.Shared.Contracts.UsfmVersificationDiagnosticType.Invalid => $"Invalid reference {sb}",
                        Serval.Shared.Contracts.UsfmVersificationDiagnosticType.IncorrectVerseSegment =>
                            $"Incorrect verse segment {sb}",
                        Serval.Shared.Contracts.UsfmVersificationDiagnosticType.UnsupportedVerseRange =>
                            $"Unsupported verse range {sb}",
                        _ => $"USFM versification issue {sb}",
                    }
                );
            }
        }

        foreach (
            (
                string parallelCorpusId,
                string monolingualCorpusId,
                MissingParentProjectErrorContract error
            ) in ParallelCorpusService.FindMissingParentProjects(parallelCorpora)
        )
        {
            warnings.Add(
                $"Unable to locate parent project {error.ParentProjectName} of daughter project {error.ProjectName} (parallel corpus {parallelCorpusId}, monolingual corpus {monolingualCorpusId})"
            );
        }

        return warnings;
    }
}
