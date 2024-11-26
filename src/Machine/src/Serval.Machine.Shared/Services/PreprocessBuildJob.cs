namespace Serval.Machine.Shared.Services;

public class PreprocessBuildJob(
    IPlatformService platformService,
    IRepository<TranslationEngine> engines,
    IDataAccessContext dataAccessContext,
    ILogger<PreprocessBuildJob> logger,
    IBuildJobService buildJobService,
    ISharedFileService sharedFileService,
    IParallelCorpusPreprocessingService parallelCorpusPreprocessingService
)
    : HangfireBuildJob<IReadOnlyList<ParallelCorpus>>(
        platformService,
        engines,
        dataAccessContext,
        buildJobService,
        logger
    )
{
    private static readonly JsonWriterOptions PretranslateWriterOptions = new() { Indented = true };

    internal BuildJobRunnerType TrainJobRunnerType { get; init; } = BuildJobRunnerType.ClearML;

    private readonly ISharedFileService _sharedFileService = sharedFileService;

    private readonly IParallelCorpusPreprocessingService _parallelCorpusPreprocessingService =
        parallelCorpusPreprocessingService;

    protected override async Task DoWorkAsync(
        string engineId,
        string buildId,
        IReadOnlyList<ParallelCorpus> data,
        string? buildOptions,
        CancellationToken cancellationToken
    )
    {
        TranslationEngine? engine = await Engines.GetAsync(e => e.EngineId == engineId, cancellationToken);
        if (engine is null)
            throw new OperationCanceledException($"Engine {engineId} does not exist.  Build canceled.");

        bool sourceTagInBaseModel = ResolveLanguageCodeForBaseModel(engine.SourceLanguage, out string srcLang);
        bool targetTagInBaseModel = ResolveLanguageCodeForBaseModel(engine.TargetLanguage, out string trgLang);

        (int trainCount, int pretranslateCount) = await WriteDataFilesAsync(
            buildId,
            data,
            buildOptions,
            cancellationToken
        );
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

        if (trainCount == 0 && (!sourceTagInBaseModel || !targetTagInBaseModel))
        {
            throw new InvalidOperationException(
                $"At least one language code in build {buildId} is unknown to the base model, and the data specified for training was empty. Build canceled."
            );
        }

        cancellationToken.ThrowIfCancellationRequested();

        bool canceling = !await BuildJobService.StartBuildJobAsync(
            TrainJobRunnerType,
            engine.Type,
            engineId,
            buildId,
            BuildStage.Train,
            buildOptions: buildOptions,
            cancellationToken: cancellationToken
        );
        if (canceling)
            throw new OperationCanceledException();
    }

    private async Task<(int TrainCount, int PretranslateCount)> WriteDataFilesAsync(
        string buildId,
        IReadOnlyList<ParallelCorpus> corpora,
        string? buildOptions,
        CancellationToken cancellationToken
    )
    {
        JsonObject? buildOptionsObject = null;
        if (buildOptions is not null)
            buildOptionsObject = JsonSerializer.Deserialize<JsonObject>(buildOptions);

        using MemoryStream sourceStream = new();
        using MemoryStream targetStream = new();
        using MemoryStream pretranslationStream = new();

        using StreamWriter targetTrainWriter = new(targetStream, Encoding.Default);
        using StreamWriter sourceTrainWriter = new(sourceStream, Encoding.Default);
        await using Utf8JsonWriter pretranslateWriter = new(pretranslationStream, PretranslateWriterOptions);

        int trainCount = 0;
        int pretranslateCount = 0;
        pretranslateWriter.WriteStartArray();
        _parallelCorpusPreprocessingService.Preprocess(
            corpora,
            row =>
            {
                if (row.SourceSegment.Length > 0 || row.TargetSegment.Length > 0)
                {
                    sourceTrainWriter.WriteLine(row.SourceSegment);
                    targetTrainWriter.WriteLine(row.TargetSegment);
                }
                if (row.SourceSegment.Length > 0 && row.TargetSegment.Length > 0)
                    trainCount++;
            },
            (row, corpus) =>
            {
                if (row.SourceSegment.Length > 0 && row.TargetSegment.Length == 0)
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
            },
            (bool?)buildOptionsObject?["use_key_terms"] ?? true
        );

        pretranslateWriter.WriteEndArray();

        await sourceTrainWriter.FlushAsync(cancellationToken);
        await targetTrainWriter.FlushAsync(cancellationToken);
        await pretranslateWriter.FlushAsync(cancellationToken);

        async Task WriteStreamAsync(MemoryStream stream, string path)
        {
            stream.Position = 0;
            await using StreamWriter writer = new(await _sharedFileService.OpenWriteAsync(path, cancellationToken));
            await writer.WriteAsync(Encoding.Default.GetString(stream.ToArray()));
            await writer.FlushAsync(cancellationToken);
        }

        await WriteStreamAsync(sourceStream, $"builds/{buildId}/train.src.txt");
        await WriteStreamAsync(targetStream, $"builds/{buildId}/train.trg.txt");
        await WriteStreamAsync(pretranslationStream, $"builds/{buildId}/pretranslate.src.json");

        return (trainCount, pretranslateCount);
    }

    protected override async Task CleanupAsync(
        string engineId,
        string buildId,
        IReadOnlyList<ParallelCorpus> data,
        JobCompletionStatus completionStatus
    )
    {
        if (completionStatus is JobCompletionStatus.Canceled)
        {
            try
            {
                await _sharedFileService.DeleteAsync($"builds/{buildId}/");
            }
            catch (Exception e)
            {
                Logger.LogWarning(e, "Unable to to delete job data for build {BuildId}.", buildId);
            }
        }
    }

    protected virtual bool ResolveLanguageCodeForBaseModel(string languageCode, out string resolvedCode)
    {
        resolvedCode = languageCode;
        return true;
    }
}
