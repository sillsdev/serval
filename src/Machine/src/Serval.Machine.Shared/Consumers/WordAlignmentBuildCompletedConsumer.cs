using Serval.WordAlignment.V1;

namespace Serval.Machine.Shared.Consumers;

public class WordAlignmentBuildCompletedConsumer(WordAlignmentPlatformApi.WordAlignmentPlatformApiClient client)
    : ServalPlatformConsumerBase<BuildCompletedRequest>(
        ServalWordAlignmentPlatformOutboxConstants.OutboxId,
        ServalWordAlignmentPlatformOutboxConstants.BuildCompleted,
        client.BuildCompletedAsync
    ) { }
