using Serval.Translation.V1;

namespace Serval.Machine.Shared.Consumers;

public class TranslationBuildCanceledConsumer(TranslationPlatformApi.TranslationPlatformApiClient client)
    : ServalPlatformConsumerBase<BuildCanceledRequest>(
        ServalTranslationPlatformOutboxConstants.OutboxId,
        ServalTranslationPlatformOutboxConstants.BuildCanceled,
        client.BuildCanceledAsync
    ) { }
