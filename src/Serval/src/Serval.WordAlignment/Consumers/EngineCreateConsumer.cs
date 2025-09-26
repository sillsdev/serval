using Serval.WordAlignment.V1;

namespace Serval.WordAlignment.Consumers;

public class EngineCreateConsumer(GrpcClientFactory grpcClientFactory)
    : OutboxConsumerBase<CreateRequest>(EngineOutboxConstants.OutboxId, EngineOutboxConstants.Create)
{
    private readonly GrpcClientFactory _grpcClientFactory = grpcClientFactory;

    protected override async Task HandleMessageAsync(
        CreateRequest content,
        Stream? stream,
        CancellationToken cancellationToken
    )
    {
        WordAlignmentEngineApi.WordAlignmentEngineApiClient client =
            _grpcClientFactory.CreateClient<WordAlignmentEngineApi.WordAlignmentEngineApiClient>(content.EngineType);
        await client.CreateAsync(content, cancellationToken: cancellationToken);
    }
}
