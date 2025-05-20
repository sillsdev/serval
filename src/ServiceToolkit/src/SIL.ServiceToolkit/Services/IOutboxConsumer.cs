namespace SIL.ServiceToolkit.Services;

public interface IOutboxConsumer
{
    public string OutboxId { get; }
    public string Method { get; }

    public Type ContentType { get; }

    public Task HandleMessageAsync(object content, Stream? stream, CancellationToken cancellationToken = default);
}
