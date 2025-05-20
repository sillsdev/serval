using Serval.Translation.V1;

namespace Serval.Machine.Shared.Consumers;

public class TranslationBuildFaultedConsumer(TranslationPlatformApi.TranslationPlatformApiClient client)
    : ServalPlatformConsumerBase<BuildFaultedRequest>(
        ServalTranslationPlatformOutboxConstants.OutboxId,
        ServalTranslationPlatformOutboxConstants.BuildFaulted,
        client.BuildFaultedAsync
    ) { }
