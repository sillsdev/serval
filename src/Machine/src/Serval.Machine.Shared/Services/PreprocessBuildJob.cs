namespace Serval.Machine.Shared.Services;

public abstract class PreprocessBuildJob<TEngine>(
    IPlatformService platformService,
    IRepository<TEngine> engines,
    IDataAccessContext dataAccessContext,
    ILogger<PreprocessBuildJob<TEngine>> logger,
    IBuildJobService<TEngine> buildJobService,
    ISharedFileService sharedFileService,
    IParallelCorpusService parallelCorpusService,
    IBuildDiagnosticService buildDiagnosticService,
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

    protected virtual BuildJobRunnerType TrainJobRunnerType { get; } = BuildJobRunnerType.ClearML;
    protected readonly BuildJobOptions BuildJobOptions = options.CurrentValue;
    protected readonly ISharedFileService SharedFileService = sharedFileService;
    protected readonly IParallelCorpusService ParallelCorpusService = parallelCorpusService;
    protected readonly IBuildDiagnosticService BuildDiagnosticService = buildDiagnosticService;

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

        PreprocessStats stats = await WriteDataFilesAsync(engineId, buildId, data, buildOptions, cancellationToken);
        bool isNonPersistedTranslationEngine = engine is TranslationEngine { IsModelPersisted: false };

        await UpdateBuildExecutionData(
            engineId,
            buildId,
            stats,
            engine.SourceLanguage,
            engine.TargetLanguage,
            isNonPersistedTranslationEngine,
            data,
            cancellationToken
        );

        await UpdateTargetQuoteConventionAsync(engineId, buildId, data, cancellationToken);

        if (stats.InferenceCount == 0 && isNonPersistedTranslationEngine)
        {
            throw new InvalidOperationException(
                $"There was no data specified for inferencing in build {buildId} and the model is not persisted. Build canceled."
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
        PreprocessStats stats,
        string sourceLanguageTag,
        string targetLanguageTag,
        bool isNonPersistedTranslationEngine,
        IReadOnlyList<ParallelCorpusContract> parallelCorpora,
        CancellationToken cancellationToken
    );

    protected virtual Task UpdateTargetQuoteConventionAsync(
        string engineId,
        string buildId,
        IReadOnlyList<ParallelCorpusContract> parallelCorpora,
        CancellationToken cancellationToken
    ) => Task.CompletedTask;

    protected abstract Task<PreprocessStats> WriteDataFilesAsync(
        string engineId,
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

    protected virtual IReadOnlyList<BuildDiagnostic> GetDiagnostics(
        int trainCount,
        int inferenceCount,
        string sourceLanguageTag,
        string targetLanguageTag,
        bool sourceLanguageHasNativeSupport,
        bool targetLanguageHasNativeSupport,
        bool isNonPersistedTranslationEngine,
        IReadOnlyList<ParallelCorpusContract> parallelCorpora
    )
    {
        List<DiagnosticContract> diagnostics = [];
        Dictionary<string, string> projectVersifications = [];

        foreach (
            (
                string parallelCorpusId,
                string monolingualCorpusId,
                string projectName,
                string projectGuid,
                string versificationName,
                IReadOnlyList<UsfmVersificationDiagnosticContract> usfmDiagnostics
            ) in ParallelCorpusService.AnalyzeUsfmVersification(parallelCorpora)
        )
        {
            projectVersifications[projectGuid] = versificationName;
            foreach (UsfmVersificationDiagnosticContract usfmDiagnostic in usfmDiagnostics)
            {
                diagnostics.Add(
                    usfmDiagnostic.Type switch
                    {
                        Serval.Shared.Contracts.UsfmVersificationDiagnosticType.InvalidChapter =>
                            BuildDiagnosticService.CreateDiagnostic(
                                "USFM-0001",
                                new Dictionary<string, object>
                                {
                                    { "projectName", projectName },
                                    { "projectGuid", projectGuid },
                                    { "usfmFilename", usfmDiagnostic.Filename },
                                    {
                                        "lineNumber",
                                        usfmDiagnostic.LineNumbers.Count > 0 ? usfmDiagnostic.LineNumbers[0] : -1
                                    },
                                    {
                                        "verseReference",
                                        usfmDiagnostic.References.Count > 0 ? usfmDiagnostic.References[0] : ""
                                    },
                                    { "parallelCorpusId", parallelCorpusId },
                                    { "monolingualCorpusId", monolingualCorpusId },
                                }
                            ),
                        Serval.Shared.Contracts.UsfmVersificationDiagnosticType.InvalidVerse =>
                            BuildDiagnosticService.CreateDiagnostic(
                                "USFM-0002",
                                new Dictionary<string, object>
                                {
                                    { "projectName", projectName },
                                    { "projectGuid", projectGuid },
                                    { "usfmFilename", usfmDiagnostic.Filename },
                                    {
                                        "lineNumber",
                                        usfmDiagnostic.LineNumbers.Count > 0 ? usfmDiagnostic.LineNumbers[0] : -1
                                    },
                                    {
                                        "verseReference",
                                        usfmDiagnostic.References.Count > 0 ? usfmDiagnostic.References[0] : ""
                                    },
                                    { "parallelCorpusId", parallelCorpusId },
                                    { "monolingualCorpusId", monolingualCorpusId },
                                }
                            ),

                        Serval.Shared.Contracts.UsfmVersificationDiagnosticType.Extra =>
                            BuildDiagnosticService.CreateDiagnostic(
                                "USFM-0003",
                                new Dictionary<string, object>
                                {
                                    { "numberOfVerses", usfmDiagnostic.NumAffectedVerses },
                                    { "projectName", projectName },
                                    { "projectGuid", projectGuid },
                                    { "usfmFilename", usfmDiagnostic.Filename },
                                    { "lineNumbers", usfmDiagnostic.LineNumbers.ToList() },
                                    { "verseReferences", usfmDiagnostic.References.ToList() },
                                    { "parallelCorpusId", parallelCorpusId },
                                    { "monolingualCorpusId", monolingualCorpusId },
                                }
                            ),
                        Serval.Shared.Contracts.UsfmVersificationDiagnosticType.Missing =>
                            BuildDiagnosticService.CreateDiagnostic(
                                "USFM-0004",
                                new Dictionary<string, object>
                                {
                                    { "numberOfVerses", usfmDiagnostic.NumAffectedVerses },
                                    { "projectName", projectName },
                                    { "projectGuid", projectGuid },
                                    { "usfmFilename", usfmDiagnostic.Filename },
                                    { "lineNumbers", usfmDiagnostic.LineNumbers.ToList() },
                                    { "verseReferences", usfmDiagnostic.References.ToList() },
                                    { "parallelCorpusId", parallelCorpusId },
                                    { "monolingualCorpusId", monolingualCorpusId },
                                }
                            ),
                        Serval.Shared.Contracts.UsfmVersificationDiagnosticType.IncorrectVerseSegment =>
                            BuildDiagnosticService.CreateDiagnostic(
                                "USFM-0005",
                                new Dictionary<string, object>
                                {
                                    { "projectName", projectName },
                                    { "projectGuid", projectGuid },
                                    { "usfmFilename", usfmDiagnostic.Filename },
                                    {
                                        "lineNumber",
                                        usfmDiagnostic.LineNumbers.Count > 0 ? usfmDiagnostic.LineNumbers[0] : -1
                                    },
                                    {
                                        "verseReference",
                                        usfmDiagnostic.References.Count > 0 ? usfmDiagnostic.References[0] : ""
                                    },
                                    { "parallelCorpusId", parallelCorpusId },
                                    { "monolingualCorpusId", monolingualCorpusId },
                                }
                            ),
                        Serval.Shared.Contracts.UsfmVersificationDiagnosticType.UnsupportedVerseRange =>
                            BuildDiagnosticService.CreateDiagnostic(
                                "USFM-0006",
                                new Dictionary<string, object>
                                {
                                    { "projectName", projectName },
                                    { "projectGuid", projectGuid },
                                    { "usfmFilename", usfmDiagnostic.Filename },
                                    {
                                        "lineNumber",
                                        usfmDiagnostic.LineNumbers.Count > 0 ? usfmDiagnostic.LineNumbers[0] : -1
                                    },
                                }
                            ),
                        _ => throw new InvalidEnumArgumentException(nameof(usfmDiagnostic.Type)),
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
            diagnostics.Add(
                BuildDiagnosticService.CreateDiagnostic(
                    "CONFIG-0001",
                    new Dictionary<string, object>
                    {
                        { "parentProjectName", error.ParentProjectName },
                        { "parentProjectGuid", error.ParentProjectGuid },
                        { "daughterProjectName", error.ProjectName },
                        { "daughterProjectGuid", error.ProjectGuid },
                        { "parallelCorpusId", parallelCorpusId },
                        { "monolingualCorpusId", monolingualCorpusId },
                    }
                )
            );
        }

        if (projectVersifications.Values.Distinct().Count() > 1)
        {
            diagnostics.Add(
                BuildDiagnosticService.CreateDiagnostic(
                    "CONFIG-0002",
                    new Dictionary<string, object> { { "projectVersifications", projectVersifications } }
                )
            );
        }

        if (inferenceCount == 0 && isNonPersistedTranslationEngine)
        {
            diagnostics.Add(BuildDiagnosticService.CreateDiagnostic("CONFIG-0004", []));
        }
        return diagnostics
            .Select(d => new BuildDiagnostic
            {
                Code = d.Code,
                Category = d.Category,
                Message = d.Message,
                Severity = (BuildDiagnosticSeverity)d.Severity,
                Data = d.Data,
            })
            .ToList();
    }

    protected static (bool IsTrainFilteredByChapter, bool IsInferenceFilteredByChapter) CheckChapterFilters(
        IReadOnlyList<ParallelCorpusContract> parallelCorpora
    )
    {
        bool isTrainFilteredByChapter = parallelCorpora.Any(pc =>
            pc.SourceCorpora.Any(c =>
                c.TrainOnChapters is not null && c.TrainOnChapters.Values.Any(chapters => chapters.Count > 0)
            )
            || pc.TargetCorpora.Any(c =>
                c.TrainOnChapters is not null && c.TrainOnChapters.Values.Any(chapters => chapters.Count > 0)
            )
        );
        bool isInferenceFilteredByChapter = parallelCorpora.Any(pc =>
            pc.SourceCorpora.Any(c =>
                c.InferenceChapters is not null && c.InferenceChapters.Values.Any(chapters => chapters.Count > 0)
            )
        );

        return (isTrainFilteredByChapter, isInferenceFilteredByChapter);
    }
}
