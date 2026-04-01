using Serval.Shared.Contracts;
using Serval.Translation.Contracts;

namespace Serval.Machine.Shared.Services;

public class ServalTranslationPlatformService(ITranslationPlatformService platformService) : IPlatformService
{
    public EngineGroup EngineGroup => EngineGroup.Translation;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new PretranslationConverter() },
    };

    private readonly ITranslationPlatformService _platformService = platformService;

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
                ?.Select(p => new BuildPhaseContract
                {
                    Stage = (Serval.Shared.Contracts.BuildPhaseStage)p.Stage,
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
        Stream pretranslationsStream,
        CancellationToken cancellationToken = default
    )
    {
        await _platformService.InsertPretranslationsAsync(
            engineId,
            ReadPretranslationsAsync(pretranslationsStream, cancellationToken),
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
                PretranslateCount = executionData.PretranslateCount,
                Warnings = executionData.Warnings,
                EngineSourceLanguageTag = executionData.EngineSourceLanguageTag,
                EngineTargetLanguageTag = executionData.EngineTargetLanguageTag,
                ResolvedSourceLanguage = executionData.ResolvedSourceLanguage,
                ResolvedTargetLanguage = executionData.ResolvedTargetLanguage,
            },
            cancellationToken
        );

    public Task UpdateTargetQuoteConventionAsync(
        string engineId,
        string buildId,
        string quoteConvention,
        CancellationToken cancellationToken = default
    ) => _platformService.UpdateTargetQuoteConventionAsync(engineId, buildId, quoteConvention, cancellationToken);

    private static async IAsyncEnumerable<PretranslationContract> ReadPretranslationsAsync(
        Stream stream,
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        await foreach (
            Pretranslation? pretranslation in JsonSerializer
                .DeserializeAsyncEnumerable<Pretranslation>(stream, JsonSerializerOptions, cancellationToken)
                .WithCancellation(cancellationToken)
        )
        {
            if (pretranslation is null)
                continue;

            yield return new PretranslationContract
            {
                CorpusId = pretranslation.CorpusId,
                TextId = pretranslation.TextId,
                SourceRefs = pretranslation.SourceRefs,
                TargetRefs = pretranslation.TargetRefs,
                Translation = pretranslation.Translation,
                SourceTokens = pretranslation.SourceTokens?.ToList(),
                TranslationTokens = pretranslation.TranslationTokens?.ToList(),
                Alignment = pretranslation
                    .Alignment?.Select(a => new AlignedWordPairContract
                    {
                        SourceIndex = a.SourceIndex,
                        TargetIndex = a.TargetIndex,
                    })
                    .ToList(),
            };
        }
    }

    private sealed class PretranslationConverter : JsonConverter<Pretranslation>
    {
        public override Pretranslation Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options
        )
        {
            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException(
                    $"Expected StartObject token at the beginning of Pretranslation object but instead encountered {reader.TokenType}"
                );
            }
            string corpusId = "",
                textId = "",
                translation = "";
            IReadOnlyList<string> sourceRefs = [],
                targetRefs = [],
                sourceTokens = [],
                translationTokens = [];
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
                        case "translation":
                            reader.Read();
                            translation = reader.GetString()!;
                            break;
                        case "sourceTokens":
                            reader.Read();
                            sourceTokens = JsonSerializer.Deserialize<IList<string>>(ref reader, options)!.ToArray();
                            break;
                        case "translationTokens":
                            reader.Read();
                            translationTokens = JsonSerializer
                                .Deserialize<IList<string>>(ref reader, options)!
                                .ToArray();
                            break;
                        case "alignment":
                            reader.Read();
                            alignedWordPairs = SIL.Machine.Corpora.AlignedWordPair.Parse(reader.GetString()).ToArray();
                            break;
                        default:
                            throw new JsonException(
                                $"Unexpected property name {s} when deserializing Pretranslation object"
                            );
                    }
                }
            }
            return new Pretranslation
            {
                CorpusId = corpusId,
                TextId = textId,
                SourceRefs = sourceRefs,
                TargetRefs = targetRefs,
                Translation = translation,
                Alignment = alignedWordPairs,
                SourceTokens = sourceTokens,
                TranslationTokens = translationTokens,
            };
        }

        public override void Write(Utf8JsonWriter writer, Pretranslation value, JsonSerializerOptions options) =>
            throw new NotSupportedException();
    }
}
