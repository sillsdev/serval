namespace Serval.Shared.Services;

public interface IEventBroker
{
    Task PublishAsync<T>(T @event, CancellationToken cancellationToken = default);
}
