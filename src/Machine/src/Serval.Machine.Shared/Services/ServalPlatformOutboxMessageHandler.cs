using Serval.Translation.V1;

namespace Serval.Machine.Shared.Services;

public class ServalPlatformOutboxMessageHandler(TranslationPlatformApi.TranslationPlatformApiClient client)
    : IOutboxMessageHandler
{
    private readonly TranslationPlatformApi.TranslationPlatformApiClient _client = client;
    private readonly JsonSerializerOptions _jsonSerializerOptions = MessageOutboxOptions.JsonSerializerOptions;

    public string OutboxId => ServalPlatformOutboxConstants.OutboxId;

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
            case ServalPlatformOutboxConstants.BuildStarted:
                await _client.BuildStartedAsync(
                    JsonSerializer.Deserialize<BuildStartedRequest>(content!, _jsonSerializerOptions),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalPlatformOutboxConstants.BuildCompleted:
                await _client.BuildCompletedAsync(
                    JsonSerializer.Deserialize<BuildCompletedRequest>(content!, _jsonSerializerOptions),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalPlatformOutboxConstants.BuildCanceled:
                await _client.BuildCanceledAsync(
                    JsonSerializer.Deserialize<BuildCanceledRequest>(content!, _jsonSerializerOptions),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalPlatformOutboxConstants.BuildFaulted:
                await _client.BuildFaultedAsync(
                    JsonSerializer.Deserialize<BuildFaultedRequest>(content!, _jsonSerializerOptions),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalPlatformOutboxConstants.BuildRestarting:
                await _client.BuildRestartingAsync(
                    JsonSerializer.Deserialize<BuildRestartingRequest>(content!, _jsonSerializerOptions),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalPlatformOutboxConstants.InsertPretranslations:
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
            case ServalPlatformOutboxConstants.IncrementTranslationEngineCorpusSize:
                await _client.IncrementTranslationEngineCorpusSizeAsync(
                    JsonSerializer.Deserialize<IncrementTranslationEngineCorpusSizeRequest>(
                        content!,
                        _jsonSerializerOptions
                    ),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalPlatformOutboxConstants.UpdateBuildExecutionData:
                await _client.UpdateBuildExecutionDataAsync(
                    JsonSerializer.Deserialize<UpdateBuildExecutionDataRequest>(content!, _jsonSerializerOptions),
                    cancellationToken: cancellationToken
                );
                break;
            default:
                throw new InvalidOperationException($"Encountered a message with the unrecognized method '{method}'.");
        }
    }
}
