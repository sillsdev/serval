using Serval.Translation.V1;

namespace Serval.Translation.Consumers;

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
        TranslationEngineApi.TranslationEngineApiClient client =
            _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(content.EngineType);
        await client.CancelBuildAsync(content, cancellationToken: cancellationToken);
    }
}
