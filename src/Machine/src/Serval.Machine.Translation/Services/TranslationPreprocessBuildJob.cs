namespace Serval.Machine.Translation.Services;

public class TranslationPreprocessBuildJob(
    [FromKeyedServices(EngineGroup.Translation)] IPlatformService platformService,
    IRepository<TranslationEngine> engines,
    IDataAccessContext dataAccessContext,
    ILogger<PreprocessBuildJob<TranslationEngine>> logger,
    IBuildJobService<TranslationEngine> buildJobService,
    ISharedFileService sharedFileService,
    IParallelCorpusService parallelCorpusService,
    IBuildDiagnosticService buildDiagnosticService,
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
    protected override async Task<PreprocessStats> WriteDataFilesAsync(
        string engineId,
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

        (bool isTrainFilteredByChapter, bool isPretranslateFilteredByChapter) = CheckChapterFilters(parallelCorpora);
        PreprocessStats preprocessStats = new()
        {
            IsTrainFilteredByChapter = isTrainFilteredByChapter,
            IsInferenceFilteredByChapter = isPretranslateFilteredByChapter,
        };

        pretranslateWriter.WriteStartArray();
        await ParallelCorpusService.PreprocessAsync(
            parallelCorpora,
            async (row, trainingDataType) =>
                await preprocessStats.ProcessTranslationTrainingRowAsync(
                    row,
                    trainingDataType,
                    sourceTrainWriter,
                    targetTrainWriter,
                    sourceKeyTermsTrainWriter,
                    targetKeyTermsTrainWriter
                ),
            async (row, isInTrainingData, corpusId) =>
                await preprocessStats.ProcessPretranslateRowAsync(row, isInTrainingData, corpusId, pretranslateWriter),
            (bool?)buildOptionsObject?["use_key_terms"] ?? true,
            ignoreUsfmMarkers: ["rem", "r"]
        );

        pretranslateWriter.WriteEndArray();

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
            (await Engines.GetAsync(e => e.EngineId == engineId, cancellationToken))?.CurrentBuild?.BaseModel.ToString()
            ?? "Unknown";
        IReadOnlyList<DiagnosticContract> diagnostics = GetDiagnostics(
            stats.TrainCount,
            stats.InferenceCount,
            sourceLanguageTag,
            targetLanguageTag,
            true,
            true,
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
            Diagnostics = diagnostics,
            DiagnosticsTruncated = diagnosticsTruncated,
            EngineSourceLanguageTag = sourceLanguageTag,
            EngineTargetLanguageTag = targetLanguageTag,
        };
        await PlatformService.UpdateBuildExecutionDataAsync(engineId, buildId, executionData, cancellationToken);
    }
}

public static class PreprocessStatsExtensions
{
    public static async Task ProcessTranslationTrainingRowAsync(
        this PreprocessStats stats,
        ParallelRowContract row,
        TrainingDataType trainingDataType,
        StreamWriter sourceTrainWriter,
        StreamWriter targetTrainWriter,
        StreamWriter sourceKeyTermsTrainWriter,
        StreamWriter targetKeyTermsTrainWriter
    )
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
            stats.UpdateTrainCount(row);
    }

    public static async Task ProcessPretranslateRowAsync(
        this PreprocessStats stats,
        ParallelRowContract row,
        bool isInTrainingData,
        string corpusId,
        Utf8JsonWriter pretranslateWriter
    )
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

            stats.UpdateInferenceCount(row);
        }
        if (pretranslateWriter.BytesPending > 1024 * 1024)
            await pretranslateWriter.FlushAsync();
    }
}
