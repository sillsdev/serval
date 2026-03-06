using Serval.Shared.Models;

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
    protected override async Task<(int TrainCount, int InferenceCount)> WriteDataFilesAsync(
        string buildId,
        IReadOnlyList<FilteredParallelCorpus> corpora,
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

        int trainCount = 0;
        int pretranslateCount = 0;
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
                    trainCount++;
            },
            async (row, isInTrainingData, corpusId) =>
            {
                if (row.SourceSegment.Length > 0 && !isInTrainingData)
                {
                    pretranslateWriter.WriteStartObject();
                    pretranslateWriter.WriteString("corpusId", corpusId);
                    pretranslateWriter.WriteString("textId", row.TextId);
                    pretranslateWriter.WriteStartArray("refs");
                    foreach (object rowRef in row.TargetRefs)
                        pretranslateWriter.WriteStringValue(rowRef.ToString());
                    pretranslateWriter.WriteEndArray();
                    pretranslateWriter.WriteString("translation", row.SourceSegment);
                    pretranslateWriter.WriteEndObject();
                    pretranslateCount++;
                }
                if (pretranslateWriter.BytesPending > 1024 * 1024)
                    await pretranslateWriter.FlushAsync();
            },
            (bool?)buildOptionsObject?["use_key_terms"] ?? true,
            ignoreUsfmMarkers: ["rem", "r"]
        );

        pretranslateWriter.WriteEndArray();

        return (trainCount, pretranslateCount);
    }

    protected override async Task UpdateBuildExecutionData(
        string engineId,
        string buildId,
        int trainCount,
        int pretranslateCount,
        string sourceLanguageTag,
        string targetLanguageTag,
        IReadOnlyList<FilteredParallelCorpus> corpora,
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
        };
        await PlatformService.UpdateBuildExecutionDataAsync(engineId, buildId, executionData, cancellationToken);
    }
}
