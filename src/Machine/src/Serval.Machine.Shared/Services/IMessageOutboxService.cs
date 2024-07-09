namespace Serval.Machine.Shared.Services;

public interface IMessageOutboxService
{
    public Task<string> EnqueueMessageAsync(
        string outboxId,
        string method,
        string groupId,
        string? content = null,
        Stream? contentStream = null,
        CancellationToken cancellationToken = default
    );
}
