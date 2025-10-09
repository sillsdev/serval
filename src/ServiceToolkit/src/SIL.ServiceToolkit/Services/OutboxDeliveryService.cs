namespace SIL.ServiceToolkit.Services;

public class OutboxDeliveryService(
    IServiceProvider services,
    IFileSystem fileSystem,
    IOptionsMonitor<OutboxOptions> options,
    ILogger<OutboxDeliveryService> logger
) : BackgroundService
{
    private readonly IServiceProvider _services = services;
    private readonly IFileSystem _fileSystem = fileSystem;
    private readonly IOptionsMonitor<OutboxOptions> _options = options;
    private readonly ILogger<OutboxDeliveryService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Initialize();
        using IServiceScope scope = _services.CreateScope();
        var messages = scope.ServiceProvider.GetRequiredService<IRepository<OutboxMessage>>();
        Dictionary<(string, string), IOutboxConsumer> consumers = scope
            .ServiceProvider.GetServices<IOutboxConsumer>()
            .ToDictionary(o => (o.OutboxId, o.Method));
        await ProcessMessagesAsync(consumers, messages, stoppingToken);
        using ISubscription<OutboxMessage> subscription = await messages.SubscribeAsync(e => true, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await subscription.WaitForChangeAsync(cancellationToken: stoppingToken);
                if (stoppingToken.IsCancellationRequested)
                    break;
                await ProcessMessagesAsync(consumers, messages, stoppingToken);
            }
            catch (TimeoutException e)
            {
                _logger.LogWarning(e, "Change stream interrupted, trying again...");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (OperationCanceledException e)
            {
                _logger.LogInformation(e, "Cancellation requested, service is stopping...");
                break;
            }
        }
    }

    private void Initialize()
    {
        _fileSystem.CreateDirectory(_options.CurrentValue.OutboxDir);
    }

    internal async Task ProcessMessagesAsync(
        Dictionary<(string, string), IOutboxConsumer> consumers,
        IRepository<OutboxMessage> messages,
        CancellationToken cancellationToken = default
    )
    {
        bool anyMessages = await messages.ExistsAsync(m => true, cancellationToken);
        if (!anyMessages)
            return;

        IReadOnlyList<OutboxMessage> curMessages = await messages.GetAllAsync(cancellationToken);

        IEnumerable<IGrouping<(string GroupId, string OutboxRef), OutboxMessage>> messageGroups = curMessages
            .OrderBy(m => m.Index)
            .GroupBy(m => (m.OutboxRef, m.GroupId));

        foreach (IGrouping<(string OutboxId, string GroupId), OutboxMessage> messageGroup in messageGroups)
        {
            bool abortMessageGroup = false;
            foreach (OutboxMessage message in messageGroup)
            {
                try
                {
                    await ProcessGroupMessagesAsync(consumers, messages, message, cancellationToken);
                }
                catch (RpcException e)
                {
                    switch (e.StatusCode)
                    {
                        case StatusCode.Unavailable:
                        case StatusCode.Unauthenticated:
                        case StatusCode.PermissionDenied:
                        case StatusCode.Cancelled:
                            _logger.LogWarning(e, "Platform Message sending failure: {statusCode}", e.StatusCode);
                            return;
                        case StatusCode.Aborted:
                        case StatusCode.DeadlineExceeded:
                        case StatusCode.Internal:
                        case StatusCode.ResourceExhausted:
                        case StatusCode.Unknown:
                            abortMessageGroup = !await CheckIfFinalMessageAttempt(messages, message, e);
                            break;
                        case StatusCode.InvalidArgument:
                        default:
                            // log error
                            await PermanentlyFailedMessage(messages, message, e);
                            break;
                    }
                }
                catch (Exception e)
                {
                    await PermanentlyFailedMessage(messages, message, e);
                    break;
                }
                if (abortMessageGroup)
                    break;
            }
        }
    }

    private async Task ProcessGroupMessagesAsync(
        Dictionary<(string, string), IOutboxConsumer> consumers,
        IRepository<OutboxMessage> messages,
        OutboxMessage message,
        CancellationToken cancellationToken = default
    )
    {
        string filePath = Path.Combine(_options.CurrentValue.OutboxDir, message.Id);
        IOutboxConsumer consumer = consumers[(message.OutboxRef, message.Method)];
        object content = OutboxService.DeserializeContent(message.Content, consumer.ContentType);
        if (message.HasContentStream)
        {
            using Stream stream = _fileSystem.OpenRead(filePath);
            await consumer.HandleMessageAsync(content, stream, cancellationToken);
        }
        else
        {
            await consumer.HandleMessageAsync(content, null, cancellationToken);
        }
        await messages.DeleteAsync(message.Id, CancellationToken.None);
        _fileSystem.DeleteFile(filePath);
    }

    private async Task<bool> CheckIfFinalMessageAttempt(
        IRepository<OutboxMessage> messages,
        OutboxMessage message,
        Exception e
    )
    {
        if (message.Created < DateTimeOffset.UtcNow.Subtract(_options.CurrentValue.MessageExpirationTimeout))
        {
            await PermanentlyFailedMessage(messages, message, e);
            return true;
        }
        else
        {
            await LogFailedAttempt(messages, message, e);
            return false;
        }
    }

    private async Task PermanentlyFailedMessage(IRepository<OutboxMessage> messages, OutboxMessage message, Exception e)
    {
        // log error
        _logger.LogError(
            e,
            "Permanently failed to process message {Id}: {Method} with content {Content} and error message: {ErrorMessage}",
            message.Id,
            message.Method,
            message.Content,
            e.Message
        );
        await messages.DeleteAsync(message.Id);
    }

    private async Task LogFailedAttempt(IRepository<OutboxMessage> messages, OutboxMessage message, Exception e)
    {
        // log error
        await messages.UpdateAsync(m => m.Id == message.Id, b => b.Inc(m => m.Attempts, 1));
        _logger.LogError(
            e,
            "Attempt {Attempts}.  Failed to process message {Id}: {Method} with content {Content} and error message: {ErrorMessage}",
            message.Attempts + 1,
            message.Id,
            message.Method,
            message.Content,
            e.Message
        );
    }
}
