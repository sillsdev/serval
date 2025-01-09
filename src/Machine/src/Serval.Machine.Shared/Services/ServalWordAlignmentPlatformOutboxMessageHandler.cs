using Serval.WordAlignment.V1;

namespace Serval.Machine.Shared.Services;

public class ServalWordAlignmentPlatformOutboxMessageHandler(
    WordAlignmentPlatformApi.WordAlignmentPlatformApiClient client
) : IOutboxMessageHandler
{
    private readonly WordAlignmentPlatformApi.WordAlignmentPlatformApiClient _client = client;
    private static readonly JsonSerializerOptions JsonSerializerOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public string OutboxId => ServalWordAlignmentPlatformOutboxConstants.OutboxId;

    public async Task HandleMessageAsync(
        string method,
        string? content,
        Stream? contentStream,
        CancellationToken cancellationToken = default
    )
    {
        switch (method)
        {
            case ServalWordAlignmentPlatformOutboxConstants.BuildStarted:
                await _client.BuildStartedAsync(
                    JsonSerializer.Deserialize<BuildStartedRequest>(content!),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalWordAlignmentPlatformOutboxConstants.BuildCompleted:
                await _client.BuildCompletedAsync(
                    JsonSerializer.Deserialize<BuildCompletedRequest>(content!),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalWordAlignmentPlatformOutboxConstants.BuildCanceled:
                await _client.BuildCanceledAsync(
                    JsonSerializer.Deserialize<BuildCanceledRequest>(content!),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalWordAlignmentPlatformOutboxConstants.BuildFaulted:
                await _client.BuildFaultedAsync(
                    JsonSerializer.Deserialize<BuildFaultedRequest>(content!),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalWordAlignmentPlatformOutboxConstants.BuildRestarting:
                await _client.BuildRestartingAsync(
                    JsonSerializer.Deserialize<BuildRestartingRequest>(content!),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalWordAlignmentPlatformOutboxConstants.InsertInferences:
                var jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerOptions);
                jsonSerializerOptions.Converters.Add(new WordAlignmentJsonConverter());
                IAsyncEnumerable<Models.WordAlignment> wordAlignments = JsonSerializer
                    .DeserializeAsyncEnumerable<Models.WordAlignment>(
                        contentStream!,
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
                                EngineId = content!,
                                CorpusId = wordAlignment.CorpusId,
                                TextId = wordAlignment.TextId,
                                Refs = { wordAlignment.Refs },
                                SourceTokens = { wordAlignment.SourceTokens },
                                TargetTokens = { wordAlignment.TargetTokens },
                                Confidences = { wordAlignment.Confidences },
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
                await _client.IncrementTrainEngineCorpusSizeAsync(
                    JsonSerializer.Deserialize<IncrementTrainEngineCorpusSizeRequest>(content!),
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
                TargetIndex = alignedWordPair.TargetIndex
            };
        }
    }
}

public class WordAlignmentJsonConverter : JsonConverter<object>
{
    public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.True:
                return true;
            case JsonTokenType.False:
                return false;
            case JsonTokenType.Number when reader.TryGetInt64(out long l):
                return l;
            case JsonTokenType.Number:
                return reader.GetDouble();
            case JsonTokenType.String:
                var str = reader.GetString();
                if (SIL.Machine.Corpora.AlignedWordPair.TryParse(str, out var alignedWordPair))
                    return alignedWordPair;
                return str!;
            default:
                throw new JsonException();
        }
    }

    public override void Write(Utf8JsonWriter writer, object objectToWrite, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, objectToWrite, objectToWrite.GetType(), options);
}
