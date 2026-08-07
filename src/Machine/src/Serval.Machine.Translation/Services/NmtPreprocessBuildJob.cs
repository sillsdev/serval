namespace Serval.Machine.Translation.Services;

public class NmtPreprocessBuildJob(
    [FromKeyedServices(EngineGroup.Translation)] IPlatformService platformService,
    IRepository<TranslationEngine> engines,
    IDataAccessContext dataAccessContext,
    ILogger<NmtPreprocessBuildJob> logger,
    IBuildJobService<TranslationEngine> buildJobService,
    ISharedFileService sharedFileService,
    ILanguageTagService languageTagService,
    IParallelCorpusService parallelCorpusService,
    IBuildDiagnosticService buildDiagnosticService,
    IOptionsMonitor<BuildJobOptions> options
)
    : TranslationPreprocessBuildJob(
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
    private readonly ILanguageTagService _languageTagService = languageTagService;

    private bool ResolveLanguageCode(string languageCode, out string resolvedCode)
    {
        return _languageTagService.ConvertToFlores200Code(languageCode, out resolvedCode)
            == Flores200Support.LanguageAndScript;
    }

    protected override async Task UpdateTargetQuoteConventionAsync(
        string engineId,
        string buildId,
        IReadOnlyList<ParallelCorpusContract> parallelCorpora,
        CancellationToken cancellationToken
    )
    {
        string overallTargetQuoteConventionAnalysis = ParallelCorpusService.AnalyzeTargetQuoteConvention(
            parallelCorpora
        );

        await PlatformService.UpdateTargetQuoteConventionAsync(
            engineId,
            buildId,
            overallTargetQuoteConventionAnalysis,
            cancellationToken
        );
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
        bool sourceLanguageHasNativeSupport = ResolveLanguageCode(sourceLanguageTag, out string resolvedSourceLanguage);
        bool targetLanguageHasNativeSupport = ResolveLanguageCode(targetLanguageTag, out string resolvedTargetLanguage);

        string modelName =
            (
                await Engines.GetAsync(e => e.EngineId == engineId, cancellationToken)
            )?.CurrentBuild?.BaseModel?.ToString() ?? "Unknown";
        IReadOnlyList<DiagnosticContract> diagnostics = GetDiagnostics(
            stats.TrainCount,
            stats.InferenceCount,
            sourceLanguageTag,
            targetLanguageTag,
            sourceLanguageHasNativeSupport,
            targetLanguageHasNativeSupport,
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
        JsonObject buildPreprocessSummary = new()
        {
            { "Event", "BuildPreprocess" },
            { "EngineId", engineId },
            { "BuildId", buildId },
            { "NumTrainRows", stats.TrainCount },
            { "NumPretranslateRows", stats.InferenceCount },
            { "EngineSourceLanguageTag", sourceLanguageTag },
            { "EngineTargetLanguageTag", targetLanguageTag },
            { "SourceLanguageResolved", resolvedSourceLanguage },
            { "TargetLanguageResolved", resolvedTargetLanguage },
            { "Warnings", new JsonArray(warnings.Select(w => JsonValue.Create(w)).ToArray()) },
        };
        Logger.LogInformation("{summary}", buildPreprocessSummary.ToJsonString());
        var executionData = new BuildExecutionData()
        {
            TrainCount = stats.TrainCount,
            InferenceCount = stats.InferenceCount,
            TrainVerseCount = stats.TrainVerseCount,
            InferenceVerseCount = stats.InferenceVerseCount,
            IsTrainFilteredByChapter = stats.IsTrainFilteredByChapter,
            IsInferenceFilteredByChapter = stats.IsInferenceFilteredByChapter,
            Warnings = warnings,
            Diagnostics = diagnostics,
            DiagnosticsTruncated = diagnosticsTruncated,
            EngineSourceLanguageTag = sourceLanguageTag,
            EngineTargetLanguageTag = targetLanguageTag,
            ResolvedSourceLanguage = resolvedSourceLanguage,
            ResolvedTargetLanguage = resolvedTargetLanguage,
        };
        await PlatformService.UpdateBuildExecutionDataAsync(engineId, buildId, executionData, cancellationToken);

        if (stats.TrainCount == 0 && (!sourceLanguageHasNativeSupport || !targetLanguageHasNativeSupport))
        {
            throw new InvalidOperationException(
                $"At least one language code in build {buildId} is unknown to the base model {modelName}, and no data was specified for training. Build canceled."
            );
        }
    }

    protected override IReadOnlyList<DiagnosticContract> GetDiagnostics(
        int trainCount,
        int inferenceCount,
        string sourceLanguageTag,
        string targetLanguageTag,
        bool sourceLanguageHasNativeSupport,
        bool targetLanguageHasNativeSupport,
        bool isNonPersistedTranslationEngine,
        string modelName,
        IReadOnlyList<ParallelCorpusContract> parallelCorpora
    )
    {
        List<DiagnosticContract> diagnostics = [];

        // Has at least a Gospel of Mark amount of data and not the special case of no data which will be caught elsewhere
        if (trainCount < 600 && trainCount != 0)
        {
            diagnostics.Add(
                BuildDiagnosticService.CreateDiagnostic(
                    "CONFIG-0003",
                    new Dictionary<string, object>
                    {
                        { "trainCount", trainCount },
                        { "minimumTrainCount", BuildJobOptions.MinimumTrainCount },
                    }
                )
            );
        }

        if (
            _languageTagService.ConvertToFlores200Code(sourceLanguageTag, out string resolvedCode)
            == Flores200Support.None
        )
        {
            diagnostics.Add(
                BuildDiagnosticService.CreateDiagnostic(
                    "MODEL-0001",
                    new Dictionary<string, object> { { "resolvedCode", resolvedCode }, { "modelName", modelName } }
                )
            );
        }

        if (_languageTagService.ConvertToFlores200Code(targetLanguageTag, out resolvedCode) == Flores200Support.None)
        {
            diagnostics.Add(
                BuildDiagnosticService.CreateDiagnostic(
                    "MODEL-0002",
                    new Dictionary<string, object> { { "resolvedCode", resolvedCode }, { "modelName", modelName } }
                )
            );
        }

        if (trainCount == 0 && (!sourceLanguageHasNativeSupport || !targetLanguageHasNativeSupport))
        {
            List<string> unknownLanguageCodes = new[]
            {
                !sourceLanguageHasNativeSupport ? sourceLanguageTag : "",
                !targetLanguageHasNativeSupport ? targetLanguageTag : "",
            }
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            diagnostics.Add(
                BuildDiagnosticService.CreateDiagnostic(
                    "MODEL-0004",
                    new Dictionary<string, object>
                    {
                        { "unknownLanguageCodes", unknownLanguageCodes },
                        { "modelName", modelName },
                    }
                )
            );
        }

        return
        [
            .. base.GetDiagnostics(
                trainCount,
                inferenceCount,
                sourceLanguageTag,
                targetLanguageTag,
                sourceLanguageHasNativeSupport,
                targetLanguageHasNativeSupport,
                isNonPersistedTranslationEngine,
                modelName,
                parallelCorpora
            ),
            .. diagnostics,
        ];
    }
}
