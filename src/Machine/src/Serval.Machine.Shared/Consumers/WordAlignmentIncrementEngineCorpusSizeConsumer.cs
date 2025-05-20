using Serval.WordAlignment.V1;

namespace Serval.Machine.Shared.Consumers;

public class WordAlignmentIncrementEngineCorpusSizeConsumer(
    WordAlignmentPlatformApi.WordAlignmentPlatformApiClient client
)
    : ServalPlatformConsumerBase<IncrementEngineCorpusSizeRequest>(
        ServalWordAlignmentPlatformOutboxConstants.OutboxId,
        ServalWordAlignmentPlatformOutboxConstants.IncrementTrainEngineCorpusSize,
        client.IncrementEngineCorpusSizeAsync
    ) { }
