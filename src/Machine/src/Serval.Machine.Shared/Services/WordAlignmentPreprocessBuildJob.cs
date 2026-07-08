using SIL.Extensions;

namespace Serval.Machine.Shared.Services;

public class WordAlignmentPreprocessBuildJob(
    [FromKeyedServices(EngineGroup.WordAlignment)] IPlatformService platformService,
    IRepository<WordAlignmentEngine> engines,
    IDataAccessContext dataAccessContext,
    ILogger<WordAlignmentPreprocessBuildJob> logger,
    IBuildJobService<WordAlignmentEngine> buildJobService,
    ISharedFileService sharedFileService,
    IParallelCorpusService parallelCorpusService,
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
    protected override async Task<PreprocessStats> WriteDataFilesAsync(
        string buildId,
        IReadOnlyList<ParallelCorpusContract> parallelCorpora,
        string? buildOptions,
        CancellationToken cancellationToken
    )
    {
        JsonObject? buildOptionsObject = null;
        if (buildOptions is not null)
            buildOptionsObject = JsonSerializer.Deserialize<JsonObject>(buildOptions);

        await using StreamWriter sourceTrainWriter = new(
            await SharedFileService.OpenWriteAsync($"builds/{buildId}/train.src.txt", cancellationToken)
        );
        await using StreamWriter targetTrainWriter = new(
            await SharedFileService.OpenWriteAsync($"builds/{buildId}/train.trg.txt", cancellationToken)
        );

        await using StreamWriter sourceKeyTermsTrainWriter = new(
            await SharedFileService.OpenWriteAsync($"builds/{buildId}/train.key-terms.src.txt", cancellationToken)
        );
        await using StreamWriter targetKeyTermsTrainWriter = new(
            await SharedFileService.OpenWriteAsync($"builds/{buildId}/train.key-terms.trg.txt", cancellationToken)
        );

        await using Stream wordAlignmentStream = await SharedFileService.OpenWriteAsync(
            $"builds/{buildId}/word_alignments.inputs.json",
            cancellationToken
        );
        await using Utf8JsonWriter wordAlignmentWriter = new(wordAlignmentStream, InferenceWriterOptions);
        bool isTrainFilteredByChapter = parallelCorpora.Any(pc =>
            pc.SourceCorpora.Any(c =>
                c.TrainOnChapters is not null && c.TrainOnChapters.Values.Any(chapters => chapters.Count > 0)
            )
        );
        bool isWordAlignmentFilteredByChapter = parallelCorpora.Any(pc =>
            pc.SourceCorpora.Any(c =>
                c.InferenceChapters is not null && c.InferenceChapters.Values.Any(chapters => chapters.Count > 0)
            )
        );

        PreprocessStats preprocessStats = new()
        {
            IsTrainFilteredByChapter = isTrainFilteredByChapter,
            IsInferenceFilteredByChapter = isWordAlignmentFilteredByChapter,
        };

        wordAlignmentWriter.WriteStartArray();
        await ParallelCorpusService.PreprocessAsync(
            parallelCorpora,
            async (row, trainingDataType) =>
                await preprocessStats.ProcessWordAlignmentTrainingRowAsync(
                    row,
                    trainingDataType,
                    sourceTrainWriter,
                    targetTrainWriter,
                    sourceKeyTermsTrainWriter,
                    targetKeyTermsTrainWriter
                ),
            async (row, isInTrainingData, corpusId) =>
                await preprocessStats.ProcessWordAlignRowAsync(row, isInTrainingData, corpusId, wordAlignmentWriter),
            (bool?)buildOptionsObject?["use_key_terms"] ?? true
        );

        wordAlignmentWriter.WriteEndArray();

        return preprocessStats;
    }

    protected override async Task UpdateBuildExecutionData(
        string engineId,
        string buildId,
        PreprocessStats stats,
        string sourceLanguageTag,
        string targetLanguageTag,
        IReadOnlyList<ParallelCorpusContract> parallelCorpora,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<string> warnings = GetWarnings(
            stats.TrainCount,
            stats.InferenceCount,
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
            { "NumTrainRows", stats.TrainCount },
            { "NumWordAlignRows", stats.InferenceCount },
            { "EngineSourceLanguageTag", sourceLanguageTag },
            { "EngineTargetLanguageTag", targetLanguageTag },
            { "Warnings", new JsonArray(warnings.Select(w => JsonValue.Create(w)).ToArray()) },
        };
        Logger.LogInformation("{summary}", buildPreprocessSummary.ToJsonString());
        var executionData = new BuildExecutionData()
        {
            TrainCount = stats.TrainCount,
            InferenceCount = stats.InferenceCount,
            TrainVerseCount = stats.TrainVerseCount.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyDictionary<string, int>)kv.Value
            ),
            InferenceVerseCount = stats.InferenceVerseCount.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyDictionary<string, int>)kv.Value
            ),
            IsInferenceFilteredByChapter = stats.IsInferenceFilteredByChapter,
            IsTrainFilteredByChapter = stats.IsTrainFilteredByChapter,
            Warnings = warnings,
            EngineSourceLanguageTag = sourceLanguageTag,
            EngineTargetLanguageTag = targetLanguageTag,
        };
        await PlatformService.UpdateBuildExecutionDataAsync(engineId, buildId, executionData, cancellationToken);
    }

    protected override Task UpdateTargetQuoteConventionAsync(
        string engineId,
        string buildId,
        IReadOnlyList<ParallelCorpusContract> parallelCorpora,
        CancellationToken cancellationToken
    )
    {
        // Word alignment does not support parallel corpus analysis
        return Task.CompletedTask;
    }
}

public static partial class PreprocessStatsExtensions
{
    public static async Task ProcessWordAlignmentTrainingRowAsync(
        this PreprocessStats stats,
        ParallelRowContract row,
        TrainingDataType trainingDataType,
        StreamWriter sourceTrainWriter,
        StreamWriter targetTrainWriter,
        StreamWriter sourceKeyTermsTrainWriter,
        StreamWriter targetKeyTermsTrainWriter
    )
    {
        if (row.SourceSegment.Length > 0 && row.TargetSegment.Length > 0)
        {
            if (trainingDataType == TrainingDataType.KeyTerm)
            {
                await sourceKeyTermsTrainWriter.WriteAsync($"{row.SourceSegment}\n");
                await targetKeyTermsTrainWriter.WriteAsync($"{row.TargetSegment}\n");
            }
            else
            {
                await sourceTrainWriter.WriteAsync($"{row.SourceSegment}\n");
                await targetTrainWriter.WriteAsync($"{row.TargetSegment}\n");
            }

            stats.TrainCount++;
            foreach (object? reference in row.SourceRefs)
            {
                if (reference is not null and ScriptureRef sr && sr.IsVerse)
                {
                    stats.TrainVerseCount.UpdateValue(
                        sr.Book,
                        () => [],
                        chapters =>
                        {
                            if (chapters.TryGetValue(sr.Chapter, out int count))
                                chapters[sr.Chapter] = count + 1;
                            else
                                chapters[sr.Chapter] = 1;
                            return chapters;
                        }
                    );
                }
            }
        }
    }

    public static async Task ProcessWordAlignRowAsync(
        this PreprocessStats stats,
        ParallelRowContract row,
        bool isInTrainingData,
        string corpusId,
        Utf8JsonWriter wordAlignmentWriter
    )
    {
        if (row.SourceSegment.Length > 0 && row.TargetSegment.Length > 0 && !isInTrainingData)
        {
            wordAlignmentWriter.WriteStartObject();
            wordAlignmentWriter.WriteString("corpusId", corpusId);
            wordAlignmentWriter.WriteString("textId", row.TextId);
            wordAlignmentWriter.WriteStartArray("sourceRefs");
            foreach (object rowRef in row.SourceRefs)
                wordAlignmentWriter.WriteStringValue(rowRef.ToString());
            wordAlignmentWriter.WriteEndArray();
            wordAlignmentWriter.WriteStartArray("targetRefs");
            foreach (object rowRef in row.TargetRefs)
                wordAlignmentWriter.WriteStringValue(rowRef.ToString());
            wordAlignmentWriter.WriteEndArray();
            wordAlignmentWriter.WriteString("source", row.SourceSegment);
            wordAlignmentWriter.WriteString("target", row.TargetSegment);
            wordAlignmentWriter.WriteEndObject();
            stats.InferenceCount++;
            foreach (object? reference in row.SourceRefs)
            {
                if (reference is not null and ScriptureRef sr && sr.IsVerse)
                {
                    stats.InferenceVerseCount.UpdateValue(
                        sr.Book,
                        () => [],
                        chapters =>
                        {
                            if (chapters.TryGetValue(sr.Chapter, out int count))
                                chapters[sr.Chapter] = count + 1;
                            else
                                chapters[sr.Chapter] = 1;
                            return chapters;
                        }
                    );
                }
            }
        }
        if (wordAlignmentWriter.BytesPending > 1024 * 1024)
            await wordAlignmentWriter.FlushAsync();
    }
}
