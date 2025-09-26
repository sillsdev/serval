using Serval.WordAlignment.V1;

namespace Serval.WordAlignment.Consumers;

public class EngineDeleteConsumer(GrpcClientFactory grpcClientFactory)
    : OutboxConsumerBase<DeleteRequest>(EngineOutboxConstants.OutboxId, EngineOutboxConstants.Delete)
{
    private readonly GrpcClientFactory _grpcClientFactory = grpcClientFactory;

    protected override async Task HandleMessageAsync(
        DeleteRequest content,
        Stream? stream,
        CancellationToken cancellationToken
    )
    {
        WordAlignmentEngineApi.WordAlignmentEngineApiClient client =
            _grpcClientFactory.CreateClient<WordAlignmentEngineApi.WordAlignmentEngineApiClient>(content.EngineType);
        await client.DeleteAsync(content, cancellationToken: cancellationToken);
    }
}
