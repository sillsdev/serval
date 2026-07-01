using SIL.Extensions;

namespace Serval.Machine.Shared.Services;

public class TranslationPreprocessBuildJob(
    [FromKeyedServices(EngineGroup.Translation)] IPlatformService platformService,
    IRepository<TranslationEngine> engines,
    IDataAccessContext dataAccessContext,
    ILogger<PreprocessBuildJob<TranslationEngine>> logger,
    IBuildJobService<TranslationEngine> buildJobService,
    ISharedFileService sharedFileService,
    IParallelCorpusService parallelCorpusService,
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

        await using Stream pretranslateStream = await SharedFileService.OpenWriteAsync(
            $"builds/{buildId}/pretranslate.src.json",
            cancellationToken
        );
        await using Utf8JsonWriter pretranslateWriter = new(pretranslateStream, InferenceWriterOptions);
        bool isTrainFilteredByChapter = parallelCorpora.Any(pc =>
            pc.SourceCorpora.Any(c =>
                c.TrainOnChapters is not null && c.TrainOnChapters.Values.Any(chapters => chapters.Count > 0)
            )
        );
        bool isPretranslationFilteredByChapter = parallelCorpora.Any(pc =>
            pc.SourceCorpora.Any(c =>
                c.InferenceChapters is not null && c.InferenceChapters.Values.Any(chapters => chapters.Count > 0)
            )
        );
        int trainCount = 0;
        int pretranslateCount = 0;
        Dictionary<string, Dictionary<string, int>> trainVerseCountByChapter = [];
        Dictionary<string, Dictionary<string, int>> pretranslateVerseCountByChapter = [];
        pretranslateWriter.WriteStartArray();
        await ParallelCorpusService.PreprocessAsync(
            parallelCorpora,
            async (row, trainingDataType) =>
            {
                if (row.SourceSegment.Length > 0 || row.TargetSegment.Length > 0)
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
                }
                if (row.SourceSegment.Length > 0 && row.TargetSegment.Length > 0)
                {
                    trainCount++;
                    foreach (object? reference in row.SourceRefs)
                    {
                        if (reference is not null and ScriptureRef sr)
                        {
                            trainVerseCountByChapter.UpdateValue(
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
            },
            async (row, isInTrainingData, corpusId) =>
            {
                if (row.SourceSegment.Length > 0 && !isInTrainingData)
                {
                    pretranslateWriter.WriteStartObject();
                    pretranslateWriter.WriteString("corpusId", corpusId);
                    pretranslateWriter.WriteString("textId", row.TextId);
                    pretranslateWriter.WriteStartArray("sourceRefs");
                    foreach (object rowRef in row.SourceRefs)
                        pretranslateWriter.WriteStringValue(rowRef.ToString());
                    pretranslateWriter.WriteEndArray();
                    pretranslateWriter.WriteStartArray("targetRefs");
                    foreach (object rowRef in row.TargetRefs)
                        pretranslateWriter.WriteStringValue(rowRef.ToString());
                    pretranslateWriter.WriteEndArray();
                    pretranslateWriter.WriteString("translation", row.SourceSegment);
                    pretranslateWriter.WriteEndObject();
                    pretranslateCount++;
                    foreach (object? reference in row.SourceRefs)
                    {
                        if (reference is not null and ScriptureRef sr)
                        {
                            pretranslateVerseCountByChapter.UpdateValue(
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
                if (pretranslateWriter.BytesPending > 1024 * 1024)
                    await pretranslateWriter.FlushAsync();
            },
            (bool?)buildOptionsObject?["use_key_terms"] ?? true,
            ignoreUsfmMarkers: ["rem", "r"]
        );

        pretranslateWriter.WriteEndArray();

        return new PreprocessStats
        {
            TrainCount = trainCount,
            InferenceCount = pretranslateCount,
            IsTrainFilteredByChapter = isTrainFilteredByChapter,
            IsInferenceFilteredByChapter = isPretranslationFilteredByChapter,
            TrainVerseCount = trainVerseCountByChapter,
            InferenceVerseCount = pretranslateVerseCountByChapter,
        };
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
            { "NumPretranslateRows", stats.InferenceCount },
            { "EngineSourceLanguageTag", sourceLanguageTag },
            { "EngineTargetLanguageTag", targetLanguageTag },
            { "Warnings", new JsonArray(warnings.Select(w => JsonValue.Create(w)).ToArray()) },
        };
        Logger.LogInformation("{summary}", buildPreprocessSummary.ToJsonString());
        var executionData = new BuildExecutionData()
        {
            TrainCount = stats.TrainCount,
            InferenceCount = stats.InferenceCount,
            IsTrainFilteredByChapter = stats.IsTrainFilteredByChapter,
            IsInferenceFilteredByChapter = stats.IsInferenceFilteredByChapter,
            TrainVerseCount = stats.TrainVerseCount,
            InferenceVerseCount = stats.InferenceVerseCount,
            Warnings = warnings,
            EngineSourceLanguageTag = sourceLanguageTag,
            EngineTargetLanguageTag = targetLanguageTag,
        };
        await PlatformService.UpdateBuildExecutionDataAsync(engineId, buildId, executionData, cancellationToken);
    }
}
