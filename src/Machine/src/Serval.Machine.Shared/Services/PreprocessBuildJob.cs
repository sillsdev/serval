namespace Serval.Machine.Shared.Services;

public class PreprocessBuildJob(
    IPlatformService platformService,
    IRepository<TranslationEngine> engines,
    IDataAccessContext dataAccessContext,
    ILogger<PreprocessBuildJob> logger,
    IBuildJobService buildJobService,
    ISharedFileService sharedFileService
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

    public ICorpusService CorpusService { get; set; } = new CorpusService();

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
        await using StreamWriter sourceTrainWriter =
            new(await _sharedFileService.OpenWriteAsync($"builds/{buildId}/train.src.txt", cancellationToken));
        await using StreamWriter targetTrainWriter =
            new(await _sharedFileService.OpenWriteAsync($"builds/{buildId}/train.trg.txt", cancellationToken));

        await using Stream pretranslateStream = await _sharedFileService.OpenWriteAsync(
            $"builds/{buildId}/pretranslate.src.json",
            cancellationToken
        );
        await using Utf8JsonWriter pretranslateWriter = new(pretranslateStream, PretranslateWriterOptions);

        int trainCount = 0;
        int pretranslateCount = 0;
        pretranslateWriter.WriteStartArray();
        new ParallelCorpusPreprocessor() { CorpusService = CorpusService }.Preprocess(
            corpora,
            row =>
            {
                sourceTrainWriter.Write($"{row.SourceSegment}\n");
                targetTrainWriter.Write($"{row.TargetSegment}\n");
                if (row.SourceSegment.Length > 0 && row.TargetSegment.Length > 0)
                    trainCount++;
            },
            (row, corpus) =>
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
            },
            (bool?)buildOptionsObject?["use_key_terms"] ?? true
        );

        pretranslateWriter.WriteEndArray();

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
