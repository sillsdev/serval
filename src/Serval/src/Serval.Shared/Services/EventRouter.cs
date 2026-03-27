using Microsoft.Extensions.DependencyInjection;

namespace Serval.Shared.Services;

public class EventRouter(IServiceProvider serviceProvider) : IEventRouter
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;

    public Task PublishAsync<TEvent>(TEvent evt, CancellationToken cancellationToken = default)
        where TEvent : IEvent
    {
        return Task.WhenAll(
            _serviceProvider
                .GetServices<IEventHandler<TEvent>>()
                .Select(handler => handler.HandleAsync(evt, cancellationToken))
        );
    }
}
