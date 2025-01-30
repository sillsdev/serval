using Serval.Translation.V1;

namespace Serval.Machine.Shared.Services;

public class ServalTranslationPlatformOutboxMessageHandler(TranslationPlatformApi.TranslationPlatformApiClient client)
    : IOutboxMessageHandler
{
    private readonly TranslationPlatformApi.TranslationPlatformApiClient _client = client;
    private readonly JsonSerializerOptions _jsonSerializerOptions = MessageOutboxOptions.JsonSerializerOptions;

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
                await _client.BuildStartedAsync(
                    JsonSerializer.Deserialize<BuildStartedRequest>(content!, _jsonSerializerOptions),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalTranslationPlatformOutboxConstants.BuildCompleted:
                await _client.BuildCompletedAsync(
                    JsonSerializer.Deserialize<BuildCompletedRequest>(content!, _jsonSerializerOptions),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalTranslationPlatformOutboxConstants.BuildCanceled:
                await _client.BuildCanceledAsync(
                    JsonSerializer.Deserialize<BuildCanceledRequest>(content!, _jsonSerializerOptions),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalTranslationPlatformOutboxConstants.BuildFaulted:
                await _client.BuildFaultedAsync(
                    JsonSerializer.Deserialize<BuildFaultedRequest>(content!, _jsonSerializerOptions),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalTranslationPlatformOutboxConstants.BuildRestarting:
                await _client.BuildRestartingAsync(
                    JsonSerializer.Deserialize<BuildRestartingRequest>(content!, _jsonSerializerOptions),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalTranslationPlatformOutboxConstants.InsertPretranslations:
                IAsyncEnumerable<Pretranslation> pretranslations = JsonSerializer
                    .DeserializeAsyncEnumerable<Pretranslation>(
                        contentStream!,
                        _jsonSerializerOptions,
                        cancellationToken
                    )
                    .OfType<Pretranslation>();

                using (var call = _client.InsertPretranslations(cancellationToken: cancellationToken))
                {
                    await foreach (Pretranslation pretranslation in pretranslations)
                    {
                        await call.RequestStream.WriteAsync(
                            new InsertPretranslationsRequest
                            {
                                EngineId = groupId,
                                CorpusId = pretranslation.CorpusId,
                                TextId = pretranslation.TextId,
                                Refs = { pretranslation.Refs },
                                Translation = pretranslation.Translation
                            },
                            cancellationToken
                        );
                    }
                    await call.RequestStream.CompleteAsync();
                    await call;
                }
                break;
            case ServalTranslationPlatformOutboxConstants.IncrementTrainEngineCorpusSize:
                await _client.IncrementTrainEngineCorpusSizeAsync(
                    JsonSerializer.Deserialize<IncrementTrainEngineCorpusSizeRequest>(content!),
                    cancellationToken: cancellationToken
                );
                break;
            default:
                throw new InvalidOperationException($"Encountered a message with the unrecognized method '{method}'.");
        }
    }
}
