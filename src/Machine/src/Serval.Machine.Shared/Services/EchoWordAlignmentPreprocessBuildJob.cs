namespace Serval.Machine.Shared.Services;

public class EchoWordAlignmentPreprocessBuildJob(
    [FromKeyedServices(EngineGroup.WordAlignment)] IPlatformService platformService,
    IRepository<WordAlignmentEngine> engines,
    IDataAccessContext dataAccessContext,
    ILogger<EchoWordAlignmentPreprocessBuildJob> logger,
    IBuildJobService<WordAlignmentEngine> buildJobService,
    ISharedFileService sharedFileService,
    IParallelCorpusService parallelCorpusService,
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
        options
    )
{
    protected override BuildJobRunnerType TrainJobRunnerType => BuildJobRunnerType.Local;

    protected override async Task<(int TrainCount, int InferenceCount)> WriteDataFilesAsync(
        string engineId,
        string buildId,
        IReadOnlyList<ParallelCorpusContract> parallelCorpora,
        string? buildOptions,
        CancellationToken cancellationToken
    )
    {
        int trainCount = 0;
        int wordAlignCount = 0;

        List<WordAlignmentContract> wordAlignments = [];
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
                    wordAlignCount++;
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }
        );

        await wordAlignmentPlatformService.InsertWordAlignmentsAsync(
            engineId,
            wordAlignments.ToAsyncEnumerable(),
            cancellationToken
        );
        return (trainCount, wordAlignCount);
    }

    protected override async Task UpdateBuildExecutionData(
        string engineId,
        string buildId,
        int trainCount,
        int wordAlignCount,
        string sourceLanguageTag,
        string targetLanguageTag,
        IReadOnlyList<ParallelCorpusContract> parallelCorpora,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<string> warnings = GetWarnings(
            trainCount,
            wordAlignCount,
            sourceLanguageTag,
            targetLanguageTag,
            parallelCorpora
        );

        // Log summary of build data
        JsonObject buildPreprocessSummary = new()
        {
            { "Event", "BuildPreprocess" },
            { "EngineId", engineId },
            { "BuildId", buildId },
            { "NumTrainRows", trainCount },
            { "NumWordAlignRows", wordAlignCount },
            { "EngineSourceLanguageTag", sourceLanguageTag },
            { "EngineTargetLanguageTag", targetLanguageTag },
            { "Warnings", new JsonArray(warnings.Select(w => JsonValue.Create(w)).ToArray()) },
        };
        Logger.LogInformation("{summary}", buildPreprocessSummary.ToJsonString());
        var executionData = new BuildExecutionData()
        {
            TrainCount = trainCount,
            WordAlignCount = wordAlignCount,
            Warnings = warnings,
            EngineSourceLanguageTag = sourceLanguageTag,
            EngineTargetLanguageTag = targetLanguageTag,
        };
        await PlatformService.UpdateBuildExecutionDataAsync(engineId, buildId, executionData, cancellationToken);
    }
}
