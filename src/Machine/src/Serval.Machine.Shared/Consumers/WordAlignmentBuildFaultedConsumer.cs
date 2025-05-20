using Serval.WordAlignment.V1;

namespace Serval.Machine.Shared.Consumers;

public class WordAlignmentBuildFaultedConsumer(WordAlignmentPlatformApi.WordAlignmentPlatformApiClient client)
    : ServalPlatformConsumerBase<BuildFaultedRequest>(
        ServalWordAlignmentPlatformOutboxConstants.OutboxId,
        ServalWordAlignmentPlatformOutboxConstants.BuildFaulted,
        client.BuildFaultedAsync
    ) { }
