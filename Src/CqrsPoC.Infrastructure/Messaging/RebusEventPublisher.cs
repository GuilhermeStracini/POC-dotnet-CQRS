using CqrsPoC.Application.Interfaces;
using Rebus.Bus;

namespace CqrsPoC.Infrastructure.Messaging;

/// <summary>
/// Wraps <see cref="IBus"/> (Rebus) to fulfil the <see cref="IEventPublisher"/>
/// contract defined in the Application layer.
/// <para>
/// Using Rebus.Publish sends the message to every subscriber of that event type.
/// The routing/subscription configuration in <see cref="DependencyInjection"/> maps
/// each event type to the correct RabbitMQ exchange/queue.
/// </para>
/// </summary>
public sealed class RebusEventPublisher(IBus bus) : IEventPublisher
{
    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : class
    {
        await bus.Publish(@event);
    }
}
