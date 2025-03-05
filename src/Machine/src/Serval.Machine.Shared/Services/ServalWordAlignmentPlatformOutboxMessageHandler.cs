using Serval.WordAlignment.V1;

namespace Serval.Machine.Shared.Services;

public class ServalWordAlignmentPlatformOutboxMessageHandler(
    WordAlignmentPlatformApi.WordAlignmentPlatformApiClient client
) : IOutboxMessageHandler
{
    private readonly WordAlignmentPlatformApi.WordAlignmentPlatformApiClient _client = client;
    private static readonly JsonSerializerOptions JsonSerializerOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new WordAlignmentConverter() } };

    public string OutboxId => ServalWordAlignmentPlatformOutboxConstants.OutboxId;

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
            case ServalWordAlignmentPlatformOutboxConstants.BuildStarted:
                ArgumentNullException.ThrowIfNull(content);
                await _client.BuildStartedAsync(
                    JsonSerializer.Deserialize<BuildStartedRequest>(content, JsonSerializerOptions),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalWordAlignmentPlatformOutboxConstants.BuildCompleted:
                ArgumentNullException.ThrowIfNull(content);
                await _client.BuildCompletedAsync(
                    JsonSerializer.Deserialize<BuildCompletedRequest>(content, JsonSerializerOptions),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalWordAlignmentPlatformOutboxConstants.BuildCanceled:
                ArgumentNullException.ThrowIfNull(content);
                await _client.BuildCanceledAsync(
                    JsonSerializer.Deserialize<BuildCanceledRequest>(content, JsonSerializerOptions),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalWordAlignmentPlatformOutboxConstants.BuildFaulted:
                ArgumentNullException.ThrowIfNull(content);
                await _client.BuildFaultedAsync(
                    JsonSerializer.Deserialize<BuildFaultedRequest>(content, JsonSerializerOptions),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalWordAlignmentPlatformOutboxConstants.BuildRestarting:
                ArgumentNullException.ThrowIfNull(content);
                await _client.BuildRestartingAsync(
                    JsonSerializer.Deserialize<BuildRestartingRequest>(content, JsonSerializerOptions),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalWordAlignmentPlatformOutboxConstants.InsertWordAlignments:
                ArgumentNullException.ThrowIfNull(contentStream);
                IAsyncEnumerable<Models.WordAlignment> wordAlignments = JsonSerializer
                    .DeserializeAsyncEnumerable<Models.WordAlignment>(
                        contentStream,
                        JsonSerializerOptions,
                        cancellationToken
                    )
                    .OfType<Models.WordAlignment>();

                using (var call = _client.InsertWordAlignments(cancellationToken: cancellationToken))
                {
                    await foreach (Models.WordAlignment wordAlignment in wordAlignments)
                    {
                        await call.RequestStream.WriteAsync(
                            new InsertWordAlignmentsRequest
                            {
                                EngineId = groupId,
                                CorpusId = wordAlignment.CorpusId,
                                TextId = wordAlignment.TextId,
                                Refs = { wordAlignment.Refs },
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
                break;
            case ServalWordAlignmentPlatformOutboxConstants.IncrementTrainEngineCorpusSize:
                ArgumentNullException.ThrowIfNull(content);
                await _client.IncrementEngineCorpusSizeAsync(
                    JsonSerializer.Deserialize<IncrementEngineCorpusSizeRequest>(content, JsonSerializerOptions),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalWordAlignmentPlatformOutboxConstants.UpdateBuildExecutionData:
                ArgumentNullException.ThrowIfNull(content);
                await _client.UpdateBuildExecutionDataAsync(
                    JsonSerializer.Deserialize<UpdateBuildExecutionDataRequest>(content),
                    cancellationToken: cancellationToken
                );
                break;
            default:
                throw new InvalidOperationException($"Encountered a message with the unrecognized method '{method}'.");
        }
    }

    private static IEnumerable<WordAlignment.V1.AlignedWordPair> Map(
        IEnumerable<SIL.Machine.Corpora.AlignedWordPair> alignedWordPairs
    )
    {
        foreach (SIL.Machine.Corpora.AlignedWordPair alignedWordPair in alignedWordPairs)
        {
            yield return new WordAlignment.V1.AlignedWordPair
            {
                SourceIndex = alignedWordPair.SourceIndex,
                TargetIndex = alignedWordPair.TargetIndex,
                Score = alignedWordPair.TranslationScore
            };
        }
    }

    internal class WordAlignmentConverter : JsonConverter<Models.WordAlignment>
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
            IReadOnlyList<string> refs = [],
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
                            refs = JsonSerializer.Deserialize<IList<string>>(ref reader, options)!.ToArray();
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
                Refs = refs,
                Alignment = alignedWordPairs,
                SourceTokens = sourceTokens,
                TargetTokens = targetTokens
            };
        }

        public override void Write(Utf8JsonWriter writer, Models.WordAlignment value, JsonSerializerOptions options) =>
            throw new NotSupportedException();
    }
}
