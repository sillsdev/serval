namespace Serval.Shared.Contracts;

public interface IEventRouter
{
    Task PublishAsync<TEvent>(TEvent evt, CancellationToken cancellationToken = default)
        where TEvent : IEvent;
}
