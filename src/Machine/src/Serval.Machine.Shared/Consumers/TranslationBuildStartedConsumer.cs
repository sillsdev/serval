using Serval.Translation.V1;

namespace Serval.Machine.Shared.Consumers;

public class TranslationBuildStartedConsumer(TranslationPlatformApi.TranslationPlatformApiClient client)
    : ServalPlatformConsumerBase<BuildStartedRequest>(
        ServalTranslationPlatformOutboxConstants.OutboxId,
        ServalTranslationPlatformOutboxConstants.BuildStarted,
        client.BuildStartedAsync
    ) { }
