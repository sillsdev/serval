using Serval.WordAlignment.V1;

namespace Serval.Machine.Shared.Consumers;

public class WordAlignmentBuildStartedConsumer(WordAlignmentPlatformApi.WordAlignmentPlatformApiClient client)
    : ServalPlatformConsumerBase<BuildStartedRequest>(
        ServalWordAlignmentPlatformOutboxConstants.OutboxId,
        ServalWordAlignmentPlatformOutboxConstants.BuildStarted,
        client.BuildStartedAsync
    ) { }
