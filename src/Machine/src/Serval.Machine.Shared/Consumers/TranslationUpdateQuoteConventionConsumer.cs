using Serval.Translation.V1;

namespace Serval.Machine.Shared.Consumers;

public class TranslationUpdateTargetQuoteConventionConsumer(TranslationPlatformApi.TranslationPlatformApiClient client)
    : ServalPlatformConsumerBase<UpdateTargetQuoteConventionRequest>(
        ServalTranslationPlatformOutboxConstants.OutboxId,
        ServalTranslationPlatformOutboxConstants.UpdateTargetQuoteConvention,
        client.UpdateTargetQuoteConventionAsync
    ) { }
