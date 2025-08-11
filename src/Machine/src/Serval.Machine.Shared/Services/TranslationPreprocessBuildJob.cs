namespace Serval.Machine.Shared.Services;

public class TranslationPreprocessBuildJob(
    [FromKeyedServices(EngineGroup.Translation)] IPlatformService platformService,
    IRepository<TranslationEngine> engines,
    IDataAccessContext dataAccessContext,
    ILogger<PreprocessBuildJob<TranslationEngine>> logger,
    IBuildJobService<TranslationEngine> buildJobService,
    ISharedFileService sharedFileService,
    IParallelCorpusPreprocessingService parallelCorpusPreprocessingService
)
    : PreprocessBuildJob<TranslationEngine>(
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

        await using Stream pretranslateStream = await SharedFileService.OpenWriteAsync(
            $"builds/{buildId}/pretranslate.src.json",
            cancellationToken
        );
        await using Utf8JsonWriter pretranslateWriter = new(pretranslateStream, InferenceWriterOptions);

        int trainCount = 0;
        int pretranslateCount = 0;
        pretranslateWriter.WriteStartArray();
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
            async (row, isInTrainingData, corpus) =>
            {
                if (row.SourceSegment.Length > 0 && !isInTrainingData)
                {
                    pretranslateWriter.WriteStartObject();
                    pretranslateWriter.WriteString("corpusId", corpus.Id);
                    pretranslateWriter.WriteString("textId", row.TextId);
                    pretranslateWriter.WriteStartArray("refs");
                    foreach (object rowRef in row.Refs)
                        pretranslateWriter.WriteStringValue(rowRef.ToString());
                    pretranslateWriter.WriteEndArray();
                    pretranslateWriter.WriteString("translation", row.SourceSegment);
                    pretranslateWriter.WriteEndObject();
                    pretranslateCount++;
                }
                if (pretranslateWriter.BytesPending > 1024 * 1024)
                    await pretranslateWriter.FlushAsync();
            },
            (bool?)buildOptionsObject?["use_key_terms"] ?? true
        );

        pretranslateWriter.WriteEndArray();

        return (trainCount, pretranslateCount);
    }

    protected override async Task UpdateBuildExecutionData(
        string engineId,
        string buildId,
        int trainCount,
        int pretranslateCount,
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
                { "NumPretranslateRows", pretranslateCount },
                { "SourceLanguageResolved", srcLang },
                { "TargetLanguageResolved", trgLang }
            };
        Logger.LogInformation("{summary}", buildPreprocessSummary.ToJsonString());
        var executionData = new Dictionary<string, string>()
        {
            { "trainCount", trainCount.ToString(CultureInfo.InvariantCulture) },
            { "pretranslateCount", pretranslateCount.ToString(CultureInfo.InvariantCulture) }
        };
        await PlatformService.UpdateBuildExecutionDataAsync(engineId, buildId, executionData, cancellationToken);
    }

    protected override async Task UpdateCorpusAnalysisAsync(
        string engineId,
        string buildId,
        IReadOnlyList<ParallelCorpus> corpora,
        CancellationToken cancellationToken
    )
    {
        List<CorpusAnalysis> corpusAnalysis = [];
        await ParallelCorpusPreprocessingService.AnalyseCorporaAsync(
            corpora,
            async (sourceQuotationConvention, targetQuotationConvention, corpus) =>
            {
                string sourceQuotationConventionName =
                    sourceQuotationConvention?.BestQuoteConvention.Name ?? string.Empty;
                string targetQuotationConventionName =
                    targetQuotationConvention?.BestQuoteConvention.Name ?? string.Empty;
                if (
                    !string.IsNullOrWhiteSpace(sourceQuotationConventionName)
                    || !string.IsNullOrWhiteSpace(sourceQuotationConventionName)
                )
                {
                    corpusAnalysis.Add(
                        new CorpusAnalysis
                        {
                            CorpusRef = corpus.Id,
                            SourceQuoteConvention = sourceQuotationConventionName,
                            TargetQuoteConvention = targetQuotationConventionName,
                        }
                    );
                }

                await Task.CompletedTask;
            }
        );
        await PlatformService.UpdateCorpusAnalysisAsync(engineId, buildId, corpusAnalysis, cancellationToken);
    }
}
