using Serval.WordAlignment.V1;

namespace Serval.Machine.Shared.Consumers;

public class WordAlignmentBuildRestartingConsumer(WordAlignmentPlatformApi.WordAlignmentPlatformApiClient client)
    : ServalPlatformConsumerBase<BuildRestartingRequest>(
        ServalWordAlignmentPlatformOutboxConstants.OutboxId,
        ServalWordAlignmentPlatformOutboxConstants.BuildRestarting,
        client.BuildRestartingAsync
    ) { }
