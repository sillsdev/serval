using Serval.WordAlignment.V1;

namespace Serval.WordAlignment.Consumers;

public class EngineStartBuildConsumer(GrpcClientFactory grpcClientFactory)
    : OutboxConsumerBase<StartBuildRequest>(EngineOutboxConstants.OutboxId, EngineOutboxConstants.StartBuild)
{
    private readonly GrpcClientFactory _grpcClientFactory = grpcClientFactory;

    protected override async Task HandleMessageAsync(
        StartBuildRequest content,
        Stream? stream,
        CancellationToken cancellationToken
    )
    {
        WordAlignmentEngineApi.WordAlignmentEngineApiClient client =
            _grpcClientFactory.CreateClient<WordAlignmentEngineApi.WordAlignmentEngineApiClient>(content.EngineType);
        await client.StartBuildAsync(content, cancellationToken: cancellationToken);
    }
}
