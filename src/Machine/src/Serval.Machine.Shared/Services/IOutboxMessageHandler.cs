namespace Serval.Machine.Shared.Services;

public interface IOutboxMessageHandler
{
    public string OutboxId { get; }

    public Task HandleMessageAsync(
        string groupId,
        string method,
        string? content,
        Stream? contentStream,
        CancellationToken cancellationToken = default
    );
}
