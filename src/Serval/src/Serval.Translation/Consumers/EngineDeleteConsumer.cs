using Serval.Translation.V1;

namespace Serval.Translation.Consumers;

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
        TranslationEngineApi.TranslationEngineApiClient client =
            _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(content.EngineType);
        await client.DeleteAsync(content, cancellationToken: cancellationToken);
    }
}
