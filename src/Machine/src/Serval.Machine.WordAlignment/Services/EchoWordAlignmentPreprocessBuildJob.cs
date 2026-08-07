namespace Serval.Machine.WordAlignment.Services;

public class EchoWordAlignmentPreprocessBuildJob(
    [FromKeyedServices(EngineGroup.WordAlignment)] IPlatformService platformService,
    IRepository<WordAlignmentEngine> engines,
    IDataAccessContext dataAccessContext,
    ILogger<EchoWordAlignmentPreprocessBuildJob> logger,
    IBuildJobService<WordAlignmentEngine> buildJobService,
    ISharedFileService sharedFileService,
    IParallelCorpusService parallelCorpusService,
    IBuildDiagnosticService buildDiagnosticService,
    IWordAlignmentPlatformService wordAlignmentPlatformService,
    IOptionsMonitor<BuildJobOptions> options
)
    : PreprocessBuildJob<WordAlignmentEngine>(
        platformService,
        engines,
        dataAccessContext,
        logger,
        buildJobService,
        sharedFileService,
        parallelCorpusService,
        buildDiagnosticService,
        options
    )
{
    protected override BuildJobRunnerType TrainJobRunnerType => BuildJobRunnerType.Local;

    protected override async Task<PreprocessStats> WriteDataFilesAsync(
        string engineId,
        string buildId,
        IReadOnlyList<ParallelCorpusContract> parallelCorpora,
        string? buildOptions,
        CancellationToken cancellationToken
    )
    {
        (bool isTrainFilteredByChapter, bool isPretranslateFilteredByChapter) = CheckChapterFilters(parallelCorpora);
        var preprocessStats = new PreprocessStats
        {
            IsTrainFilteredByChapter = isTrainFilteredByChapter,
            IsInferenceFilteredByChapter = isPretranslateFilteredByChapter,
        };

        List<WordAlignmentContract> wordAlignments = [];
        await ParallelCorpusService.PreprocessAsync(
            parallelCorpora,
            (row, _) =>
            {
                if (row.SourceSegment.Length > 0 && row.TargetSegment.Length > 0)
                    preprocessStats.UpdateTrainCount(row);
                return Task.CompletedTask;
            },
            (row, isInTrainingData, corpusId) =>
            {
                string[] sourceTokens = row.SourceSegment.Split();
                string[] targetTokens = row.TargetSegment.Split();
                int minLength = Math.Min(sourceTokens.Length, targetTokens.Length);

                wordAlignments.Add(
                    new WordAlignmentContract
                    {
                        CorpusId = corpusId,
                        TextId = row.TextId,
                        SourceRefs = row.SourceRefs.Select(r => r.ToString()!).ToArray(),
                        TargetRefs = row.TargetRefs.Select(r => r.ToString()!).ToArray(),
                        SourceTokens = sourceTokens,
                        TargetTokens = targetTokens,
                        Alignment = Enumerable
                            .Range(0, minLength)
                            .Select(i => new AlignedWordPairContract { SourceIndex = i, TargetIndex = i })
                            .ToList(),
                    }
                );
                if (row.SourceSegment.Length > 0 && row.TargetSegment.Length > 0 && !isInTrainingData)
                    preprocessStats.UpdateInferenceCount(row);
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        );

        await wordAlignmentPlatformService.InsertWordAlignmentsAsync(
            engineId,
            wordAlignments.ToAsyncEnumerable(),
            cancellationToken
        );
        return preprocessStats;
    }

    protected override async Task UpdateBuildExecutionData(
        string engineId,
        string buildId,
        PreprocessStats stats,
        string sourceLanguageTag,
        string targetLanguageTag,
        bool isNonPersistedTranslationEngine,
        IReadOnlyList<ParallelCorpusContract> parallelCorpora,
        CancellationToken cancellationToken
    )
    {
        string modelName =
            (await Engines.GetAsync(e => e.EngineId == engineId, cancellationToken))?.CurrentBuild?.Model?.ToString()
            ?? "Unknown";
        IReadOnlyList<DiagnosticContract> diagnostics = GetDiagnostics(
            stats.TrainCount,
            stats.InferenceCount,
            sourceLanguageTag,
            targetLanguageTag,
            sourceLanguageHasNativeSupport: true,
            targetLanguageHasNativeSupport: true,
            isNonPersistedTranslationEngine,
            modelName,
            parallelCorpora
        );

        IReadOnlyList<string> warnings = diagnostics.Select(d => d.Message).ToList();

        int maxDiagnostics = BuildJobOptions.MaxDiagnostics;
        bool diagnosticsTruncated = false;
        if (diagnostics.Count > maxDiagnostics)
        {
            diagnosticsTruncated = true;
            diagnostics = diagnostics.OrderByDescending(d => d.Severity).Take(maxDiagnostics).ToList();
        }

        int maxWarnings = BuildJobOptions.MaxWarnings;
        if (warnings.Count > maxWarnings)
        {
            string tooManyWarningsWarning =
                $"There were {warnings.Count} warnings. Only the first {maxWarnings} are shown.";
            warnings = [tooManyWarningsWarning, .. warnings.Take(maxWarnings)];
        }

        // Log summary of build data
        var buildPreprocessSummary = new JsonObject
        {
            { "Event", "BuildPreprocess" },
            { "EngineId", engineId },
            { "BuildId", buildId },
            { "NumTrainRows", stats.TrainCount },
            { "NumPretranslateRows", stats.InferenceCount },
            { "EngineSourceLanguageTag", sourceLanguageTag },
            { "EngineTargetLanguageTag", targetLanguageTag },
            { "Warnings", new JsonArray(warnings.Select(w => JsonValue.Create(w)).ToArray()) },
        };
        Logger.LogInformation("{summary}", buildPreprocessSummary.ToJsonString());
        var executionData = new BuildExecutionData
        {
            TrainCount = stats.TrainCount,
            InferenceCount = stats.InferenceCount,
            TrainVerseCount = stats.TrainVerseCount,
            InferenceVerseCount = stats.InferenceVerseCount,
            IsInferenceFilteredByChapter = stats.IsInferenceFilteredByChapter,
            IsTrainFilteredByChapter = stats.IsTrainFilteredByChapter,
            Warnings = warnings,
            Diagnostics = diagnostics,
            DiagnosticsTruncated = diagnosticsTruncated,
            EngineSourceLanguageTag = sourceLanguageTag,
            EngineTargetLanguageTag = targetLanguageTag,
        };
        await PlatformService.UpdateBuildExecutionDataAsync(engineId, buildId, executionData, cancellationToken);
    }
}
