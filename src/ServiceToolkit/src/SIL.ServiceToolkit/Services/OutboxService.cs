namespace SIL.ServiceToolkit.Services;

public class OutboxService(
    IRepository<Outbox> outboxes,
    IRepository<OutboxMessage> messages,
    IIdGenerator idGenerator,
    IFileSystem fileSystem,
    IOptionsMonitor<OutboxOptions> options
) : IOutboxService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions =
        new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    static OutboxService()
    {
        JsonSerializerOptions.AddProtobufSupport();
    }

    public static string SerializeContent(object content)
    {
        return JsonSerializer.Serialize(content, JsonSerializerOptions);
    }

    public static object DeserializeContent(string content, Type type)
    {
        object? result = JsonSerializer.Deserialize(content, type, JsonSerializerOptions);
        if (result == null)
            throw new InvalidOperationException("The JSON content cannot be null.");
        return result;
    }

    private readonly IRepository<Outbox> _outboxes = outboxes;
    private readonly IRepository<OutboxMessage> _messages = messages;
    private readonly IIdGenerator _idGenerator = idGenerator;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly IOptionsMonitor<OutboxOptions> _options = options;
    internal int MaxDocumentSize { get; set; } = 1_000_000;

    public async Task<string> EnqueueMessageAsync(
        string outboxId,
        string method,
        string groupId,
        object content,
        Stream? stream = null,
        CancellationToken cancellationToken = default
    )
    {
        string serializedContent = SerializeContent(content);
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
                HasContentStream = stream is not null
            };
        if (stream is null)
        {
            await _messages.InsertAsync(outboxMessage, cancellationToken: cancellationToken);
        }
        else
        {
            string filePath = Path.Combine(_options.CurrentValue.OutboxDir, outboxMessage.Id);
            try
            {
                await using (Stream fileStream = _fileSystem.OpenWrite(filePath))
                {
                    await stream.CopyToAsync(fileStream, cancellationToken);
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
        return outboxMessage.Id;
    }
}
