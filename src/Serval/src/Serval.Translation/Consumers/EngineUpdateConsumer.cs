using Serval.Translation.V1;

namespace Serval.Translation.Consumers;

public class EngineUpdateConsumer(GrpcClientFactory grpcClientFactory)
    : OutboxConsumerBase<UpdateRequest>(EngineOutboxConstants.OutboxId, EngineOutboxConstants.Update)
{
    private readonly GrpcClientFactory _grpcClientFactory = grpcClientFactory;

    protected override async Task HandleMessageAsync(
        UpdateRequest content,
        Stream? stream,
        CancellationToken cancellationToken
    )
    {
        TranslationEngineApi.TranslationEngineApiClient client =
            _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(content.EngineType);
        await client.UpdateAsync(content, cancellationToken: cancellationToken);
    }
}
