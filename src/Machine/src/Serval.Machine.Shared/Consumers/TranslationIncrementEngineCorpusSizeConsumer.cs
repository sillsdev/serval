using Serval.Translation.V1;

namespace Serval.Machine.Shared.Consumers;

public class TranslationIncrementEngineCorpusSizeConsumer(TranslationPlatformApi.TranslationPlatformApiClient client)
    : ServalPlatformConsumerBase<IncrementEngineCorpusSizeRequest>(
        ServalTranslationPlatformOutboxConstants.OutboxId,
        ServalTranslationPlatformOutboxConstants.IncrementEngineCorpusSize,
        client.IncrementEngineCorpusSizeAsync
    ) { }
