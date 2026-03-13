namespace Serval.Shared.Contracts;

public interface IEventHandler<in TEvent>
    where TEvent : IEvent
{
    Task HandleAsync(TEvent evt, CancellationToken cancellationToken);
}
