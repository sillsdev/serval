using Serval.WordAlignment.V1;

namespace Serval.Machine.Shared.Consumers;

public class WordAlignmentInsertWordAlignmentsConsumer(WordAlignmentPlatformApi.WordAlignmentPlatformApiClient client)
    : OutboxConsumerBase<string>(
        ServalWordAlignmentPlatformOutboxConstants.OutboxId,
        ServalWordAlignmentPlatformOutboxConstants.InsertWordAlignments
    )
{
    private static readonly JsonSerializerOptions JsonSerializerOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new WordAlignmentConverter() } };

    private readonly WordAlignmentPlatformApi.WordAlignmentPlatformApiClient _client = client;

    protected override async Task HandleMessageAsync(
        string content,
        Stream? stream,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(stream);

        IAsyncEnumerable<Models.WordAlignment> wordAlignments = JsonSerializer
            .DeserializeAsyncEnumerable<Models.WordAlignment>(stream, JsonSerializerOptions, cancellationToken)
            .OfType<Models.WordAlignment>();

        using var call = _client.InsertWordAlignments(cancellationToken: cancellationToken);
        await foreach (Models.WordAlignment wordAlignment in wordAlignments)
        {
            await call.RequestStream.WriteAsync(
                new InsertWordAlignmentsRequest
                {
                    EngineId = content,
                    CorpusId = wordAlignment.CorpusId,
                    TextId = wordAlignment.TextId,
                    SourceRefs = { wordAlignment.SourceRefs },
                    TargetRefs = { wordAlignment.TargetRefs },
                    SourceTokens = { wordAlignment.SourceTokens },
                    TargetTokens = { wordAlignment.TargetTokens },
                    Alignment = { Map(wordAlignment.Alignment) }
                },
                cancellationToken
            );
        }

        await call.RequestStream.CompleteAsync();
        await call;
    }

    private static IEnumerable<WordAlignment.V1.AlignedWordPair> Map(
        IEnumerable<SIL.Machine.Corpora.AlignedWordPair> alignedWordPairs
    )
    {
        foreach (SIL.Machine.Corpora.AlignedWordPair pair in alignedWordPairs)
        {
            yield return new WordAlignment.V1.AlignedWordPair
            {
                SourceIndex = pair.SourceIndex,
                TargetIndex = pair.TargetIndex,
                Score = pair.TranslationScore
            };
        }
    }

    private class WordAlignmentConverter : JsonConverter<Models.WordAlignment>
    {
        public override Models.WordAlignment Read(
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
                textId = "";
            IReadOnlyList<string> sourceRefs = [],
                targetRefs = [],
                sourceTokens = [],
                targetTokens = [];
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
                            alignedWordPairs = SIL.Machine.Corpora.AlignedWordPair.Parse(reader.GetString()).ToArray();
                            break;
                        default:
                            throw new JsonException(
                                $"Unexpected property name {s} when deserializing WordAlignment object"
                            );
                    }
                }
            }
            return new Models.WordAlignment()
            {
                CorpusId = corpusId,
                TextId = textId,
                SourceRefs = sourceRefs,
                TargetRefs = targetRefs,
                Alignment = alignedWordPairs,
                SourceTokens = sourceTokens,
                TargetTokens = targetTokens
            };
        }

        public override void Write(Utf8JsonWriter writer, Models.WordAlignment value, JsonSerializerOptions options) =>
            throw new NotSupportedException();
    }
}
