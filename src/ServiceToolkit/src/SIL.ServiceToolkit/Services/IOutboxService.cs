namespace SIL.ServiceToolkit.Services;

public interface IOutboxService
{
    public Task<string> EnqueueMessageAsync(
        string outboxId,
        string method,
        string groupId,
        object content,
        Stream? stream = null,
        CancellationToken cancellationToken = default
    );
}
