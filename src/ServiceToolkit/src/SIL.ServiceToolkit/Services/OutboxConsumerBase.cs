namespace SIL.ServiceToolkit.Services;

public abstract class OutboxConsumerBase<T>(string outboxId, string method) : IOutboxConsumer
{
    public string OutboxId { get; } = outboxId;

    public string Method { get; } = method;

    public Type ContentType => typeof(T);

    public Task HandleMessageAsync(object content, Stream? stream, CancellationToken cancellationToken = default)
    {
        return HandleMessageAsync((T)content, stream, cancellationToken);
    }

    protected abstract Task HandleMessageAsync(T content, Stream? stream, CancellationToken cancellationToken);
}
