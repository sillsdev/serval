using Serval.Translation.V1;

namespace Serval.Machine.Shared.Consumers;

public class TranslationBuildCompletedConsumer(TranslationPlatformApi.TranslationPlatformApiClient client)
    : ServalPlatformConsumerBase<BuildCompletedRequest>(
        ServalTranslationPlatformOutboxConstants.OutboxId,
        ServalTranslationPlatformOutboxConstants.BuildCompleted,
        client.BuildCompletedAsync
    ) { }
