using Serval.Translation.V1;

namespace Serval.Translation.Consumers;

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
        TranslationEngineApi.TranslationEngineApiClient client =
            _grpcClientFactory.CreateClient<TranslationEngineApi.TranslationEngineApiClient>(content.EngineType);
        await client.CreateAsync(content, cancellationToken: cancellationToken);
    }
}
