using Serval.Translation.V1;

namespace Serval.Translation.Consumers;

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
        TranslationEngineApi.TranslationEngineApiClient client =
            _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(content.EngineType);
        await client.StartBuildAsync(content, cancellationToken: cancellationToken);
    }
}
