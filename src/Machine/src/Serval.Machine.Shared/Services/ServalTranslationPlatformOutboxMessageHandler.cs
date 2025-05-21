using Serval.Translation.V1;

namespace Serval.Machine.Shared.Services;

public class ServalTranslationPlatformOutboxMessageHandler : IOutboxMessageHandler
{
    public ServalTranslationPlatformOutboxMessageHandler(TranslationPlatformApi.TranslationPlatformApiClient client)
    {
        _client = client;
        _jsonSerializerOptions = MessageOutboxOptions.JsonSerializerOptions;
        _jsonSerializerOptions.Converters.Add(new PretranslationConverter());
    }

    private readonly TranslationPlatformApi.TranslationPlatformApiClient _client;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public string OutboxId => ServalTranslationPlatformOutboxConstants.OutboxId;

    public async Task HandleMessageAsync(
        string groupId,
        string method,
        string? content,
        Stream? contentStream,
        CancellationToken cancellationToken = default
    )
    {
        switch (method)
        {
            case ServalTranslationPlatformOutboxConstants.BuildStarted:
                ArgumentNullException.ThrowIfNull(content);
                await _client.BuildStartedAsync(
                    JsonSerializer.Deserialize<BuildStartedRequest>(content, _jsonSerializerOptions),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalTranslationPlatformOutboxConstants.BuildCompleted:
                ArgumentNullException.ThrowIfNull(content);
                await _client.BuildCompletedAsync(
                    JsonSerializer.Deserialize<BuildCompletedRequest>(content, _jsonSerializerOptions),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalTranslationPlatformOutboxConstants.BuildCanceled:
                ArgumentNullException.ThrowIfNull(content);
                await _client.BuildCanceledAsync(
                    JsonSerializer.Deserialize<BuildCanceledRequest>(content, _jsonSerializerOptions),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalTranslationPlatformOutboxConstants.BuildFaulted:
                ArgumentNullException.ThrowIfNull(content);
                await _client.BuildFaultedAsync(
                    JsonSerializer.Deserialize<BuildFaultedRequest>(content, _jsonSerializerOptions),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalTranslationPlatformOutboxConstants.BuildRestarting:
                ArgumentNullException.ThrowIfNull(content);
                await _client.BuildRestartingAsync(
                    JsonSerializer.Deserialize<BuildRestartingRequest>(content, _jsonSerializerOptions),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalTranslationPlatformOutboxConstants.InsertPretranslations:
                ArgumentNullException.ThrowIfNull(contentStream);
                IAsyncEnumerable<Pretranslation> pretranslations = JsonSerializer
                    .DeserializeAsyncEnumerable<Pretranslation>(
                        contentStream,
                        _jsonSerializerOptions,
                        cancellationToken
                    )
                    .OfType<Pretranslation>();

                using (var call = _client.InsertPretranslations(cancellationToken: cancellationToken))
                {
                    await foreach (Pretranslation pretranslation in pretranslations)
                    {
                        InsertPretranslationsRequest request = new InsertPretranslationsRequest
                        {
                            EngineId = groupId,
                            CorpusId = pretranslation.CorpusId,
                            TextId = pretranslation.TextId,
                            Refs = { pretranslation.Refs },
                            Translation = pretranslation.Translation,
                            SourceTokens = { pretranslation.SourceTokens },
                            TranslationTokens = { pretranslation.TranslationTokens },
                        };
                        if (pretranslation.Alignment is not null)
                            request.Alignment.Add(pretranslation.Alignment.Select(Map));

                        await call.RequestStream.WriteAsync(request, cancellationToken);
                    }
                    await call.RequestStream.CompleteAsync();
                    await call;
                }
                break;
            case ServalTranslationPlatformOutboxConstants.IncrementTrainEngineCorpusSize:
                ArgumentNullException.ThrowIfNull(content);
                await _client.IncrementEngineCorpusSizeAsync(
                    JsonSerializer.Deserialize<IncrementEngineCorpusSizeRequest>(content, _jsonSerializerOptions),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalTranslationPlatformOutboxConstants.UpdateBuildExecutionData:
                ArgumentNullException.ThrowIfNull(content);
                await _client.UpdateBuildExecutionDataAsync(
                    JsonSerializer.Deserialize<UpdateBuildExecutionDataRequest>(content, _jsonSerializerOptions),
                    cancellationToken: cancellationToken
                );
                break;
            default:
                throw new InvalidOperationException($"Encountered a message with the unrecognized method '{method}'.");
        }
    }

    private static Translation.V1.AlignedWordPair Map(SIL.Machine.Corpora.AlignedWordPair alignedWordPair)
    {
        return new Translation.V1.AlignedWordPair()
        {
            SourceIndex = alignedWordPair.SourceIndex,
            TargetIndex = alignedWordPair.TargetIndex,
        };
    }

    private class PretranslationConverter : JsonConverter<Pretranslation>
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
                    $"Expected StartObject token at the beginning of WordAlignment object but instead encountered {reader.TokenType}"
                );
            }
            string corpusId = "",
                textId = "",
                translation = "";
            IReadOnlyList<string> refs = [],
                sourceTokens = [],
                translationTokens = [];
            IReadOnlyList<SIL.Machine.Corpora.AlignedWordPair> alignedWordPairs = [];
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
                            refs = JsonSerializer.Deserialize<IList<string>>(ref reader, options)!.ToArray();
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
                                $"Unexpected property name {s} when deserializing WordAlignment object"
                            );
                    }
                }
            }
            return new Pretranslation()
            {
                CorpusId = corpusId,
                TextId = textId,
                Refs = refs,
                Translation = translation,
                Alignment = alignedWordPairs,
                SourceTokens = sourceTokens,
                TranslationTokens = translationTokens
            };
        }

        public override void Write(Utf8JsonWriter writer, Pretranslation value, JsonSerializerOptions options) =>
            throw new NotSupportedException();
    }
}
