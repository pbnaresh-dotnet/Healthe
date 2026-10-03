using Microsoft.Extensions.DependencyInjection;
using HealthApp.Domain.Events;

namespace HealthApp.Application.Events;

public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent @event, CancellationToken cancellationToken = default);
}

public interface IDomainEventDispatcher
{
    Task PublishAsync(IDomainEvent @event, CancellationToken cancellationToken = default);
}

public sealed class DomainEventDispatcher(IServiceProvider services) : IDomainEventDispatcher
{
    public async Task PublishAsync(IDomainEvent @event, CancellationToken cancellationToken = default)
    {
        var eventType = @event.GetType();
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
        var handlers = services.GetServices(handlerType).ToArray();
        foreach (var handler in handlers)
        {
            var method = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!;
            await (Task)method.Invoke(handler, new object?[] { @event, cancellationToken })!;
        }
    }
}
