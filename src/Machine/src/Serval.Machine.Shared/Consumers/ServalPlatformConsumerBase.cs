namespace Serval.Machine.Shared.Consumers;

public abstract class ServalPlatformConsumerBase<T>(
    string outboxId,
    string method,
    Func<T, Metadata?, DateTime?, CancellationToken, AsyncUnaryCall<Google.Protobuf.WellKnownTypes.Empty>> platformFunc
) : OutboxConsumerBase<T>(outboxId, method)
{
    private readonly Func<
        T,
        Metadata?,
        DateTime?,
        CancellationToken,
        AsyncUnaryCall<Google.Protobuf.WellKnownTypes.Empty>
    > _platformFunc = platformFunc;

    protected override async Task HandleMessageAsync(T content, Stream? stream, CancellationToken cancellationToken)
    {
        await _platformFunc(content, null, null, cancellationToken);
    }
}
