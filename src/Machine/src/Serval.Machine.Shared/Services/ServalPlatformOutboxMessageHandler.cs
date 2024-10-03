using Serval.Base;
using Serval.Translation.V1;

namespace Serval.Machine.Shared.Services;

public class ServalPlatformOutboxMessageHandler(TranslationPlatformApi.TranslationPlatformApiClient client)
    : IOutboxMessageHandler
{
    private readonly TranslationPlatformApi.TranslationPlatformApiClient _client = client;
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
                await _client.JobStartedAsync(
                    JsonSerializer.Deserialize<JobStartedRequest>(content!),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalPlatformOutboxConstants.BuildCompleted:
                await _client.JobCompletedAsync(
                    JsonSerializer.Deserialize<JobCompletedRequest>(content!),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalPlatformOutboxConstants.BuildCanceled:
                await _client.JobCanceledAsync(
                    JsonSerializer.Deserialize<JobCanceledRequest>(content!),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalPlatformOutboxConstants.BuildFaulted:
                await _client.JobFaultedAsync(
                    JsonSerializer.Deserialize<JobFaultedRequest>(content!),
                    cancellationToken: cancellationToken
                );
                break;
            case ServalPlatformOutboxConstants.JobRestarting:
                await _client.JobRestartingAsync(
                    JsonSerializer.Deserialize<JobRestartingRequest>(content!),
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

                using (var call = _client.InsertPretranslations(cancellationToken: cancellationToken))
                {
                    await foreach (Pretranslation pretranslation in pretranslations)
                    {
                        await call.RequestStream.WriteAsync(
                            new InsertPretranslationsRequest
                            {
                                EngineId = content!,
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
                    JsonSerializer.Deserialize<IncrementTranslationEngineCorpusSizeRequest>(content!),
                    cancellationToken: cancellationToken
                );
                break;
            default:
                throw new InvalidOperationException($"Encountered a message with the unrecognized method '{method}'.");
        }
    }
}
