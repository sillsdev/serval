using Serval.WordAlignment.V1;

namespace Serval.Machine.Shared.Consumers;

public class WordAlignmentUpdateBuildExecutionDataConsumer(
    WordAlignmentPlatformApi.WordAlignmentPlatformApiClient client
)
    : ServalPlatformConsumerBase<UpdateBuildExecutionDataRequest>(
        ServalWordAlignmentPlatformOutboxConstants.OutboxId,
        ServalWordAlignmentPlatformOutboxConstants.UpdateBuildExecutionData,
        client.UpdateBuildExecutionDataAsync
    ) { }
