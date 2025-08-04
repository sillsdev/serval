using Serval.Translation.V1;

namespace Serval.Machine.Shared.Consumers;

public class TranslationUpdateCorpusAnalysisConsumer(TranslationPlatformApi.TranslationPlatformApiClient client)
    : ServalPlatformConsumerBase<UpdateCorpusAnalysisRequest>(
        ServalTranslationPlatformOutboxConstants.OutboxId,
        ServalTranslationPlatformOutboxConstants.UpdateCorpusAnalysis,
        client.UpdateCorpusAnalysisAsync
    ) { }
