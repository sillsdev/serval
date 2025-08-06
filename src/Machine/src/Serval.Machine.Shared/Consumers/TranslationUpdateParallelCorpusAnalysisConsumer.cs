using Serval.Translation.V1;

namespace Serval.Machine.Shared.Consumers;

public class TranslationUpdateParallelCorpusAnalysisConsumer(TranslationPlatformApi.TranslationPlatformApiClient client)
    : ServalPlatformConsumerBase<UpdateParallelCorpusAnalysisRequest>(
        ServalTranslationPlatformOutboxConstants.OutboxId,
        ServalTranslationPlatformOutboxConstants.UpdateParallelCorpusAnalysis,
        client.UpdateParallelCorpusAnalysisAsync
    ) { }
