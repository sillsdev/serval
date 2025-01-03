namespace Serval.Machine.Shared.Services;

public interface IMessageOutboxService
{
    public Task<string> EnqueueMessageAsync<TValue>(
        string outboxId,
        string method,
        string groupId,
        TValue content,
        CancellationToken cancellationToken = default
    );

    public Task<string> EnqueueMessageStreamAsync(
        string outboxId,
        string method,
        string groupId,
        Stream contentStream,
        CancellationToken cancellationToken = default
    );
}
