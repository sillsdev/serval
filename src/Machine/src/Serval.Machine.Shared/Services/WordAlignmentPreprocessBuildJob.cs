namespace Serval.Machine.Shared.Services;

public class WordAlignmentPreprocessBuildJob(
    IEnumerable<IPlatformService> platformServices,
    IRepository<WordAlignmentEngine> engines,
    IDataAccessContext dataAccessContext,
    ILogger<WordAlignmentPreprocessBuildJob> logger,
    IBuildJobService<WordAlignmentEngine> buildJobService,
    ISharedFileService sharedFileService,
    IParallelCorpusPreprocessingService parallelCorpusPreprocessingService
)
    : PreprocessBuildJob<WordAlignmentEngine>(
        platformServices.First(ps => ps.EngineGroup == EngineGroup.WordAlignment),
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

        await using Stream inferenceStream = await SharedFileService.OpenWriteAsync(
            $"builds/{buildId}/word_alignment_inputs.json",
            cancellationToken
        );
        await using Utf8JsonWriter inferenceWriter = new(inferenceStream, InferenceWriterOptions);

        int trainCount = 0;
        int inferenceCount = 0;
        inferenceWriter.WriteStartArray();
        await ParallelCorpusPreprocessingService.PreprocessAsync(
            corpora,
            async row =>
            {
                if (row.SourceSegment.Length > 0 || row.TargetSegment.Length > 0)
                {
                    await sourceTrainWriter.WriteAsync($"{row.SourceSegment}\n");
                    await targetTrainWriter.WriteAsync($"{row.TargetSegment}\n");
                }
                if (row.SourceSegment.Length > 0 && row.TargetSegment.Length > 0)
                    trainCount++;
            },
            async (row, corpus) =>
            {
                if (row.SourceSegment.Length > 0 && row.TargetSegment.Length > 0)
                {
                    inferenceWriter.WriteStartObject();
                    inferenceWriter.WriteString("corpusId", corpus.Id);
                    inferenceWriter.WriteString("textId", row.TextId);
                    inferenceWriter.WriteStartArray("refs");
                    foreach (object rowRef in row.Refs)
                        inferenceWriter.WriteStringValue(rowRef.ToString());
                    inferenceWriter.WriteEndArray();
                    inferenceWriter.WriteString("source", row.SourceSegment);
                    inferenceWriter.WriteString("target", row.TargetSegment);
                    inferenceWriter.WriteEndObject();
                    inferenceCount++;
                }
                if (inferenceWriter.BytesPending > 1024 * 1024)
                    await inferenceWriter.FlushAsync();
            },
            (bool?)buildOptionsObject?["use_key_terms"] ?? true
        );

        inferenceWriter.WriteEndArray();

        return (trainCount, inferenceCount);
    }
}
