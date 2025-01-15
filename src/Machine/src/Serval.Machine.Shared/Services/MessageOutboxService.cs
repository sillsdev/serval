namespace Serval.Machine.Shared.Services;

public class MessageOutboxService(
    IRepository<Outbox> outboxes,
    IRepository<OutboxMessage> messages,
    IIdGenerator idGenerator,
    IFileSystem fileSystem,
    IOptionsMonitor<MessageOutboxOptions> options
) : IMessageOutboxService
{
    private readonly IRepository<Outbox> _outboxes = outboxes;
    private readonly IRepository<OutboxMessage> _messages = messages;
    private readonly IIdGenerator _idGenerator = idGenerator;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly IOptionsMonitor<MessageOutboxOptions> _options = options;
    internal int MaxDocumentSize { get; set; } = 1_000_000;

    public async Task<string> EnqueueMessageAsync<TValue>(
        string outboxId,
        string method,
        string groupId,
        TValue content,
        CancellationToken cancellationToken = default
    )
    {
        string serializedContent = JsonSerializer.Serialize(content, MessageOutboxOptions.JsonSerializerOptions);
        if (serializedContent.Length > MaxDocumentSize)
        {
            throw new ArgumentException(
                $"The content is too large for request {method} with group ID {groupId}. "
                    + $"It is {serializedContent.Length} bytes, but the maximum is {MaxDocumentSize} bytes."
            );
        }
        Outbox outbox = (
            await _outboxes.UpdateAsync(
                outboxId,
                u => u.Inc(o => o.CurrentIndex, 1),
                upsert: true,
                cancellationToken: cancellationToken
            )
        )!;
        OutboxMessage outboxMessage =
            new()
            {
                Id = _idGenerator.GenerateId(),
                Index = outbox.CurrentIndex,
                OutboxRef = outboxId,
                Method = method,
                GroupId = groupId,
                Content = serializedContent,
                HasContentStream = false
            };
        await _messages.InsertAsync(outboxMessage, cancellationToken: cancellationToken);
        return outboxMessage.Id;
    }

    public async Task<string> EnqueueMessageStreamAsync(
        string outboxId,
        string method,
        string groupId,
        Stream contentStream,
        CancellationToken cancellationToken = default
    )
    {
        Outbox outbox = (
            await _outboxes.UpdateAsync(
                outboxId,
                u => u.Inc(o => o.CurrentIndex, 1),
                upsert: true,
                cancellationToken: cancellationToken
            )
        )!;
        OutboxMessage outboxMessage =
            new()
            {
                Id = _idGenerator.GenerateId(),
                Index = outbox.CurrentIndex,
                OutboxRef = outboxId,
                Method = method,
                GroupId = groupId,
                Content = null,
                HasContentStream = true
            };
        string filePath = Path.Combine(_options.CurrentValue.OutboxDir, outboxMessage.Id);
        try
        {
            await using (Stream fileStream = _fileSystem.OpenWrite(filePath))
            {
                await contentStream.CopyToAsync(fileStream, cancellationToken);
            }
            await _messages.InsertAsync(outboxMessage, cancellationToken: cancellationToken);
            return outboxMessage.Id;
        }
        catch
        {
            _fileSystem.DeleteFile(filePath);
            throw;
        }
    }
}
