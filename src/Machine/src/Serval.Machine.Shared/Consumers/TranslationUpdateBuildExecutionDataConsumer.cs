using Serval.Translation.V1;

namespace Serval.Machine.Shared.Consumers;

public class TranslationUpdateBuildExecutionDataConsumer(TranslationPlatformApi.TranslationPlatformApiClient client)
    : ServalPlatformConsumerBase<UpdateBuildExecutionDataRequest>(
        ServalTranslationPlatformOutboxConstants.OutboxId,
        ServalTranslationPlatformOutboxConstants.UpdateBuildExecutionData,
        client.UpdateBuildExecutionDataAsync
    ) { }
