using Serval.WordAlignment.V1;

namespace Serval.WordAlignment.Consumers;

public class EngineCancelBuildConsumer(GrpcClientFactory grpcClientFactory)
    : OutboxConsumerBase<CancelBuildRequest>(EngineOutboxConstants.OutboxId, EngineOutboxConstants.CancelBuild)
{
    private readonly GrpcClientFactory _grpcClientFactory = grpcClientFactory;

    protected override async Task HandleMessageAsync(
        CancelBuildRequest content,
        Stream? stream,
        CancellationToken cancellationToken
    )
    {
        WordAlignmentEngineApi.WordAlignmentEngineApiClient client =
            _grpcClientFactory.CreateClient<WordAlignmentEngineApi.WordAlignmentEngineApiClient>(content.EngineType);
        await client.CancelBuildAsync(content, cancellationToken: cancellationToken);
    }
}
