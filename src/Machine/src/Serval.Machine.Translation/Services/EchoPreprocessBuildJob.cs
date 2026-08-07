namespace Serval.Machine.Translation.Services;

public class EchoPreprocessBuildJob(
    [FromKeyedServices(EngineGroup.Translation)] IPlatformService platformService,
    IRepository<TranslationEngine> engines,
    IDataAccessContext dataAccessContext,
    ILogger<EchoPreprocessBuildJob> logger,
    IBuildJobService<TranslationEngine> buildJobService,
    ISharedFileService sharedFileService,
    IParallelCorpusService parallelCorpusService,
    IBuildDiagnosticService buildDiagnosticService,
    ITranslationPlatformService translationPlatformService,
    IOptionsMonitor<BuildJobOptions> options
)
    : PreprocessBuildJob<TranslationEngine>(
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
            { "SourceLanguageResolved", sourceLanguageTag },
            { "TargetLanguageResolved", targetLanguageTag },
            { "Warnings", new JsonArray(warnings.Select(w => JsonValue.Create(w)).ToArray()) },
        };
        Logger.LogInformation("{summary}", buildPreprocessSummary.ToJsonString());
        var executionData = new BuildExecutionData
        {
            TrainCount = stats.TrainCount,
            InferenceCount = stats.InferenceCount,
            IsTrainFilteredByChapter = stats.IsTrainFilteredByChapter,
            IsInferenceFilteredByChapter = stats.IsInferenceFilteredByChapter,
            TrainVerseCount = stats.TrainVerseCount,
            InferenceVerseCount = stats.InferenceVerseCount,
            Warnings = warnings,
            Diagnostics = diagnostics,
            DiagnosticsTruncated = diagnosticsTruncated,
            EngineSourceLanguageTag = sourceLanguageTag,
            EngineTargetLanguageTag = targetLanguageTag,
            ResolvedSourceLanguage = sourceLanguageTag,
            ResolvedTargetLanguage = targetLanguageTag,
        };
        await PlatformService.UpdateBuildExecutionDataAsync(engineId, buildId, executionData, cancellationToken);
    }

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

        List<PretranslationContract> pretranslations = [];
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
                string[] tokens = row.SourceSegment.Split();
                pretranslations.Add(
                    new PretranslationContract
                    {
                        CorpusId = corpusId,
                        TextId = row.TextId,
                        SourceRefs = [.. row.SourceRefs.Select(r => r.ToString()!)],
                        TargetRefs = [.. row.TargetRefs.Select(r => r.ToString()!)],
                        Translation = row.SourceSegment,
                        SourceTokens = tokens,
                        TranslationTokens = tokens,
                        Alignment =
                        [
                            .. tokens.Select(
                                (_, i) => new AlignedWordPairContract { SourceIndex = i, TargetIndex = i }
                            ),
                        ],
                        Confidence = 1.0,
                    }
                );
                if (row.SourceSegment.Length > 0 && !isInTrainingData)
                    preprocessStats.UpdateInferenceCount(row);
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        );

        await translationPlatformService.InsertPretranslationsAsync(
            engineId,
            buildId,
            pretranslations.ToAsyncEnumerable(),
            cancellationToken
        );

        return preprocessStats;
    }
}
