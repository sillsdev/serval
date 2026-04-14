using Serval.WordAlignment.Contracts;

namespace Serval.Machine.Shared.Services;

public class ServalWordAlignmentPlatformService(IWordAlignmentPlatformService platformService) : IPlatformService
{
    public EngineGroup EngineGroup => EngineGroup.WordAlignment;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new WordAlignmentConverter() },
    };

    private readonly IWordAlignmentPlatformService _platformService = platformService;

    public Task BuildStartedAsync(string buildId, CancellationToken cancellationToken = default) =>
        _platformService.BuildStartedAsync(buildId, cancellationToken);

    public Task BuildCompletedAsync(
        string buildId,
        int trainSize,
        double confidence,
        CancellationToken cancellationToken = default
    ) => _platformService.BuildCompletedAsync(buildId, trainSize, confidence, cancellationToken);

    public Task BuildCanceledAsync(string buildId, CancellationToken cancellationToken = default) =>
        _platformService.BuildCanceledAsync(buildId, cancellationToken);

    public Task BuildFaultedAsync(string buildId, string message, CancellationToken cancellationToken = default) =>
        _platformService.BuildFaultedAsync(buildId, message, cancellationToken);

    public Task BuildRestartingAsync(string buildId, CancellationToken cancellationToken = default) =>
        _platformService.BuildRestartingAsync(buildId, cancellationToken);

    public Task UpdateBuildStatusAsync(
        string buildId,
        ProgressStatus progressStatus,
        int? queueDepth = null,
        IReadOnlyCollection<BuildPhase>? phases = null,
        DateTime? started = null,
        DateTime? completed = null,
        CancellationToken cancellationToken = default
    ) =>
        _platformService.UpdateBuildStatusAsync(
            buildId,
            new BuildProgressStatusContract
            {
                Step = progressStatus.Step,
                PercentCompleted = progressStatus.PercentCompleted,
                Message = progressStatus.Message,
            },
            queueDepth,
            phases
                ?.Select(p => new PhaseContract
                {
                    Stage = (PhaseStage)p.Stage,
                    Step = p.Step,
                    StepCount = p.StepCount,
                    Started = p.Started,
                })
                .ToList(),
            started,
            completed,
            cancellationToken
        );

    public Task UpdateBuildStatusAsync(string buildId, int step, CancellationToken cancellationToken = default) =>
        _platformService.UpdateBuildStatusAsync(buildId, step, cancellationToken);

    public async Task InsertInferenceResultsAsync(
        string engineId,
        Stream wordAlignmentsStream,
        CancellationToken cancellationToken = default
    )
    {
        await _platformService.InsertWordAlignmentsAsync(
            engineId,
            ReadWordAlignmentsAsync(wordAlignmentsStream, cancellationToken),
            cancellationToken
        );
    }

    public Task IncrementTrainSizeAsync(
        string engineId,
        int count = 1,
        CancellationToken cancellationToken = default
    ) => _platformService.IncrementEngineCorpusSizeAsync(engineId, count, cancellationToken);

    public Task UpdateBuildExecutionDataAsync(
        string engineId,
        string buildId,
        BuildExecutionData executionData,
        CancellationToken cancellationToken = default
    ) =>
        _platformService.UpdateBuildExecutionDataAsync(
            engineId,
            buildId,
            new ExecutionDataContract
            {
                TrainCount = executionData.TrainCount,
                WordAlignCount = executionData.WordAlignCount,
                Warnings = executionData.Warnings,
                EngineSourceLanguageTag = executionData.EngineSourceLanguageTag,
                EngineTargetLanguageTag = executionData.EngineTargetLanguageTag,
            },
            cancellationToken
        );

    public Task UpdateTargetQuoteConventionAsync(
        string engineId,
        string buildId,
        string targetQuoteConvention,
        CancellationToken cancellationToken = default
    )
    {
        // Word alignment does not support quote convention analysis
        return Task.CompletedTask;
    }

    private static async IAsyncEnumerable<WordAlignmentContract> ReadWordAlignmentsAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        await foreach (
            Models.WordAlignment? record in JsonSerializer
                .DeserializeAsyncEnumerable<Models.WordAlignment>(stream, JsonSerializerOptions, cancellationToken)
                .WithCancellation(cancellationToken)
        )
        {
            if (record is null)
                continue;

            yield return new WordAlignmentContract
            {
                CorpusId = record.CorpusId,
                TextId = record.TextId,
                SourceRefs = record.SourceRefs,
                TargetRefs = record.TargetRefs,
                SourceTokens = record.SourceTokens,
                TargetTokens = record.TargetTokens,
                Alignment = record
                    .Alignment.Select(a => new AlignedWordPairContract
                    {
                        SourceIndex = a.SourceIndex,
                        TargetIndex = a.TargetIndex,
                        Score = a.TranslationScore,
                    })
                    .ToList(),
            };
        }
    }

    private sealed class WordAlignmentConverter : JsonConverter<Models.WordAlignment>
    {
        public override Models.WordAlignment Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        )
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException($"Expected StartObject token but instead encountered {reader.TokenType}");
            }
            string corpusId = "",
                textId = "";
            IReadOnlyList<string> sourceRefs = [],
                targetRefs = [],
                sourceTokens = [],
                targetTokens = [];
            IReadOnlyList<AlignedWordPair> alignedWordPairs = [];
            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    string s = reader.GetString()!;
                    switch (s)
                    {
                        case "corpusId":
                            reader.Read();
                            corpusId = reader.GetString()!;
                            break;
                        case "textId":
                            reader.Read();
                            textId = reader.GetString()!;
                            break;
                        case "refs":
                            reader.Read();
                            targetRefs = JsonSerializer.Deserialize<IList<string>>(ref reader, options)!.ToArray();
                            break;
                        case "sourceRefs":
                            reader.Read();
                            sourceRefs = JsonSerializer.Deserialize<IList<string>>(ref reader, options)!.ToArray();
                            break;
                        case "targetRefs":
                            reader.Read();
                            targetRefs = JsonSerializer.Deserialize<IList<string>>(ref reader, options)!.ToArray();
                            break;
                        case "sourceTokens":
                            reader.Read();
                            sourceTokens = JsonSerializer.Deserialize<IList<string>>(ref reader, options)!.ToArray();
                            break;
                        case "targetTokens":
                            reader.Read();
                            targetTokens = JsonSerializer.Deserialize<IList<string>>(ref reader, options)!.ToArray();
                            break;
                        case "alignment":
                            reader.Read();
                            alignedWordPairs = AlignedWordPair.Parse(reader.GetString()).ToArray();
                            break;
                        default:
                            throw new JsonException(
                                $"Unexpected property name {s} when deserializing WordAlignmentRecord object"
                            );
                    }
                }
            }
            return new Models.WordAlignment
            {
                CorpusId = corpusId,
                TextId = textId,
                SourceRefs = sourceRefs,
                TargetRefs = targetRefs,
                SourceTokens = sourceTokens,
                TargetTokens = targetTokens,
                Alignment = alignedWordPairs,
            };
        }

        public override void Write(Utf8JsonWriter writer, Models.WordAlignment value, JsonSerializerOptions options) =>
            throw new NotSupportedException();
    }
}
