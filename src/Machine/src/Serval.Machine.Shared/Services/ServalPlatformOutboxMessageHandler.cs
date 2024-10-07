using Serval.Engine.V1;
using Serval.Translation.V1;

namespace Serval.Machine.Shared.Services;

public class ServalPlatformOutboxMessageHandler(
    EnginePlatformApi.EnginePlatformApiClient engineClient,
    TranslationPlatformExtensionsApi.TranslationPlatformExtensionsApiClient translationClient
) : IOutboxMessageHandler
{
    private readonly EnginePlatformApi.EnginePlatformApiClient _engineClient = engineClient;
    private readonly TranslationPlatformExtensionsApi.TranslationPlatformExtensionsApiClient _translationClient =
        translationClient;
    private static readonly JsonSerializerOptions JsonSerializerOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public string OutboxId => ServalPlatformOutboxConstants.OutboxId;

    public async Task HandleMessageAsync(
        string method,
        string? content,
        Stream? contentStream,
        CancellationToken cancellationToken = default
    )
    {
        switch (method)
        {
            case ServalPlatformOutboxConstants.BuildStarted:
                await _engineClient.BuildStartedAsync(
                    JsonSerializer.Deserialize<BuildStartedRequest>(content!),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalPlatformOutboxConstants.BuildCompleted:
                await _engineClient.BuildCompletedAsync(
                    JsonSerializer.Deserialize<BuildCompletedRequest>(content!),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalPlatformOutboxConstants.BuildCanceled:
                await _engineClient.BuildCanceledAsync(
                    JsonSerializer.Deserialize<BuildCanceledRequest>(content!),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalPlatformOutboxConstants.BuildFaulted:
                await _engineClient.BuildFaultedAsync(
                    JsonSerializer.Deserialize<BuildFaultedRequest>(content!),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalPlatformOutboxConstants.BuildRestarting:
                await _engineClient.BuildRestartingAsync(
                    JsonSerializer.Deserialize<BuildRestartingRequest>(content!),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalPlatformOutboxConstants.InsertPretranslations:
                IAsyncEnumerable<Pretranslation> pretranslations = JsonSerializer
                    .DeserializeAsyncEnumerable<Pretranslation>(
                        contentStream!,
                        JsonSerializerOptions,
                        cancellationToken
                    )
                    .OfType<Pretranslation>();

                using (var call = _engineClient.InsertResults(cancellationToken: cancellationToken))
                {
                    await foreach (Pretranslation pretranslation in pretranslations)
                    {
                        await call.RequestStream.WriteAsync(
                            new InsertResultsRequest
                            {
                                EngineId = content!,
                                ContentSerialized = JsonSerializer.Serialize(
                                    new TranslationResultContent
                                    {
                                        CorpusId = pretranslation.CorpusId,
                                        TextId = pretranslation.TextId,
                                        Refs = { pretranslation.Refs },
                                        Translation = pretranslation.Translation
                                    }
                                ),
                            },
                            cancellationToken
                        );
                    }
                    await call.RequestStream.CompleteAsync();
                    await call;
                }
                break;
            case ServalPlatformOutboxConstants.IncrementTranslationEngineCorpusSize:
                await _translationClient.IncrementTranslationEngineCorpusSizeAsync(
                    JsonSerializer.Deserialize<IncrementTranslationEngineCorpusSizeRequest>(content!),
                    cancellationToken: cancellationToken
                );
                break;
            default:
                throw new InvalidOperationException($"Encountered a message with the unrecognized method '{method}'.");
        }
    }
}
