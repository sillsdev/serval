namespace Serval.Machine.Shared.Services;

public class EchoPreprocessBuildJob(
    [FromKeyedServices(EngineGroup.Translation)] IPlatformService platformService,
    IRepository<TranslationEngine> engines,
    IDataAccessContext dataAccessContext,
    ILogger<EchoPreprocessBuildJob> logger,
    IBuildJobService<TranslationEngine> buildJobService,
    ISharedFileService sharedFileService,
    IParallelCorpusService parallelCorpusService,
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
        options
    )
{
    protected override BuildJobRunnerType TrainJobRunnerType => BuildJobRunnerType.Local;

    protected override async Task UpdateBuildExecutionData(
        string engineId,
        string buildId,
        int trainCount,
        int pretranslateCount,
        string sourceLanguageTag,
        string targetLanguageTag,
        IReadOnlyList<ParallelCorpusContract> parallelCorpora,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<string> warnings = GetWarnings(
            trainCount,
            pretranslateCount,
            sourceLanguageTag,
            targetLanguageTag,
            parallelCorpora
        );

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
            { "NumTrainRows", trainCount },
            { "NumPretranslateRows", pretranslateCount },
            { "EngineSourceLanguageTag", sourceLanguageTag },
            { "EngineTargetLanguageTag", targetLanguageTag },
            { "SourceLanguageResolved", sourceLanguageTag },
            { "TargetLanguageResolved", targetLanguageTag },
            { "Warnings", new JsonArray(warnings.Select(w => JsonValue.Create(w)).ToArray()) },
        };
        Logger.LogInformation("{summary}", buildPreprocessSummary.ToJsonString());
        var executionData = new BuildExecutionData()
        {
            TrainCount = trainCount,
            PretranslateCount = pretranslateCount,
            Warnings = warnings,
            EngineSourceLanguageTag = sourceLanguageTag,
            EngineTargetLanguageTag = targetLanguageTag,
            ResolvedSourceLanguage = sourceLanguageTag,
            ResolvedTargetLanguage = targetLanguageTag,
        };
        await PlatformService.UpdateBuildExecutionDataAsync(engineId, buildId, executionData, cancellationToken);
    }

    protected override async Task<(int TrainCount, int InferenceCount)> WriteDataFilesAsync(
        string engineId,
        string buildId,
        IReadOnlyList<ParallelCorpusContract> parallelCorpora,
        string? buildOptions,
        CancellationToken cancellationToken
    )
    {
        int trainCount = 0;
        int pretranslateCount = 0;

        List<PretranslationContract> pretranslations = [];
        await ParallelCorpusService.PreprocessAsync(
            parallelCorpora,
            (row, _) =>
            {
                if (row.SourceSegment.Length > 0 && row.TargetSegment.Length > 0)
                    trainCount++;
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
                    pretranslateCount++;
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

        return (trainCount, pretranslateCount);
    }
}
