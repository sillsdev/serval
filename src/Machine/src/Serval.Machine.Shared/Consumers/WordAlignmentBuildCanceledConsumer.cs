using Serval.WordAlignment.V1;

namespace Serval.Machine.Shared.Consumers;

public class WordAlignmentBuildCanceledConsumer(WordAlignmentPlatformApi.WordAlignmentPlatformApiClient client)
    : ServalPlatformConsumerBase<BuildCanceledRequest>(
        ServalWordAlignmentPlatformOutboxConstants.OutboxId,
        ServalWordAlignmentPlatformOutboxConstants.BuildCanceled,
        client.BuildCanceledAsync
    ) { }
