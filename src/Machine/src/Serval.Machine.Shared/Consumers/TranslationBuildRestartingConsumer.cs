using Serval.Translation.V1;

namespace Serval.Machine.Shared.Consumers;

public class TranslationBuildRestartingConsumer(TranslationPlatformApi.TranslationPlatformApiClient client)
    : ServalPlatformConsumerBase<BuildRestartingRequest>(
        ServalTranslationPlatformOutboxConstants.OutboxId,
        ServalTranslationPlatformOutboxConstants.BuildRestarting,
        client.BuildRestartingAsync
    ) { }
