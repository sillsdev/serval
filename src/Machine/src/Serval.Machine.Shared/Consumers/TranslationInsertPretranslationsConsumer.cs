using Serval.Translation.V1;

namespace Serval.Machine.Shared.Consumers;

public class TranslationInsertPretranslationsConsumer(TranslationPlatformApi.TranslationPlatformApiClient client)
    : OutboxConsumerBase<string>(
        ServalTranslationPlatformOutboxConstants.OutboxId,
        ServalTranslationPlatformOutboxConstants.InsertPretranslations
    )
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new PretranslationConverter() },
    };

    private readonly TranslationPlatformApi.TranslationPlatformApiClient _client = client;

    protected override async Task HandleMessageAsync(
        string content,
        Stream? stream,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(stream);

        IAsyncEnumerable<Pretranslation> pretranslations = JsonSerializer
            .DeserializeAsyncEnumerable<Pretranslation>(stream, JsonSerializerOptions, cancellationToken)
            .OfType<Pretranslation>();

        using var call = _client.InsertPretranslations(cancellationToken: cancellationToken);
        await foreach (Pretranslation pretranslation in pretranslations)
        {
            InsertPretranslationsRequest request = new InsertPretranslationsRequest
            {
                EngineId = content,
                CorpusId = pretranslation.CorpusId,
                TextId = pretranslation.TextId,
                SourceRefs = { pretranslation.SourceRefs },
                TargetRefs = { pretranslation.TargetRefs },
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
            IReadOnlyList<string> sourceRefs = [],
                targetRefs = [],
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
                                $"Unexpected property name {s} when deserializing WordAlignment object"
                            );
                    }
                }
            }
            return new Pretranslation()
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
