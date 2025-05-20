using Serval.Translation.V1;

namespace Serval.Machine.Shared.Consumers;

public class TranslationInsertPretranslationsConsumer(TranslationPlatformApi.TranslationPlatformApiClient client)
    : OutboxConsumerBase<string>(
        ServalTranslationPlatformOutboxConstants.OutboxId,
        ServalTranslationPlatformOutboxConstants.InsertPretranslations
    )
{
    private static readonly JsonSerializerOptions JsonSerializerOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

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
            await call.RequestStream.WriteAsync(
                new InsertPretranslationsRequest
                {
                    EngineId = content,
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
}
