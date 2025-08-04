namespace Serval.Machine.Shared.Services;

public class WordAlignmentPreprocessBuildJob(
    [FromKeyedServices(EngineGroup.WordAlignment)] IPlatformService platformService,
    IRepository<WordAlignmentEngine> engines,
    IDataAccessContext dataAccessContext,
    ILogger<WordAlignmentPreprocessBuildJob> logger,
    IBuildJobService<WordAlignmentEngine> buildJobService,
    ISharedFileService sharedFileService,
    IParallelCorpusPreprocessingService parallelCorpusPreprocessingService
)
    : PreprocessBuildJob<WordAlignmentEngine>(
        platformService,
        engines,
        dataAccessContext,
        logger,
        buildJobService,
        sharedFileService,
        parallelCorpusPreprocessingService
    )
{
    protected override async Task<(int TrainCount, int InferenceCount)> WriteDataFilesAsync(
        string buildId,
        IReadOnlyList<ParallelCorpus> corpora,
        string? buildOptions,
        CancellationToken cancellationToken
    )
    {
        JsonObject? buildOptionsObject = null;
        if (buildOptions is not null)
            buildOptionsObject = JsonSerializer.Deserialize<JsonObject>(buildOptions);

        await using StreamWriter sourceTrainWriter =
            new(await SharedFileService.OpenWriteAsync($"builds/{buildId}/train.src.txt", cancellationToken));
        await using StreamWriter targetTrainWriter =
            new(await SharedFileService.OpenWriteAsync($"builds/{buildId}/train.trg.txt", cancellationToken));

        await using Stream wordAlignmentStream = await SharedFileService.OpenWriteAsync(
            $"builds/{buildId}/word_alignments.inputs.json",
            cancellationToken
        );
        await using Utf8JsonWriter wordAlignmentWriter = new(wordAlignmentStream, InferenceWriterOptions);

        int trainCount = 0;
        int inferenceCount = 0;
        wordAlignmentWriter.WriteStartArray();
        await ParallelCorpusPreprocessingService.PreprocessAsync(
            corpora,
            async row =>
            {
                if (row.SourceSegment.Length > 0 && row.TargetSegment.Length > 0)
                {
                    await sourceTrainWriter.WriteAsync($"{row.SourceSegment}\n");
                    await targetTrainWriter.WriteAsync($"{row.TargetSegment}\n");
                    trainCount++;
                }
            },
            async (row, isInTrainingData, corpus) =>
            {
                if (row.SourceSegment.Length > 0 && row.TargetSegment.Length > 0 && !isInTrainingData)
                {
                    wordAlignmentWriter.WriteStartObject();
                    wordAlignmentWriter.WriteString("corpusId", corpus.Id);
                    wordAlignmentWriter.WriteString("textId", row.TextId);
                    wordAlignmentWriter.WriteStartArray("refs");
                    foreach (object rowRef in row.Refs)
                        wordAlignmentWriter.WriteStringValue(rowRef.ToString());
                    wordAlignmentWriter.WriteEndArray();
                    wordAlignmentWriter.WriteString("source", row.SourceSegment);
                    wordAlignmentWriter.WriteString("target", row.TargetSegment);
                    wordAlignmentWriter.WriteEndObject();
                    inferenceCount++;
                }
                if (wordAlignmentWriter.BytesPending > 1024 * 1024)
                    await wordAlignmentWriter.FlushAsync();
            },
            (bool?)buildOptionsObject?["use_key_terms"] ?? true
        );

        wordAlignmentWriter.WriteEndArray();

        return (trainCount, inferenceCount);
    }

    protected override async Task UpdateBuildExecutionData(
        string engineId,
        string buildId,
        int trainCount,
        int wordAlignCount,
        string srcLang,
        string trgLang,
        CancellationToken cancellationToken
    )
    {
        // Log summary of build data
        JsonObject buildPreprocessSummary =
            new()
            {
                { "Event", "BuildPreprocess" },
                { "EngineId", engineId },
                { "BuildId", buildId },
                { "NumTrainRows", trainCount },
                { "NumWordAlignRows", wordAlignCount },
                { "SourceLanguageResolved", srcLang },
                { "TargetLanguageResolved", trgLang }
            };
        Logger.LogInformation("{summary}", buildPreprocessSummary.ToJsonString());
        var executionData = new Dictionary<string, string>()
        {
            { "trainCount", trainCount.ToString(CultureInfo.InvariantCulture) },
            { "wordAlignCount", wordAlignCount.ToString(CultureInfo.InvariantCulture) }
        };
        await PlatformService.UpdateBuildExecutionDataAsync(engineId, buildId, executionData, cancellationToken);
    }

    protected override Task UpdateCorpusAnalysisAsync(
        string engineId,
        string buildId,
        IReadOnlyList<ParallelCorpus> corpora,
        CancellationToken cancellationToken
    )
    {
        // Word alignment does not support corpus analysis
        return Task.CompletedTask;
    }
}
