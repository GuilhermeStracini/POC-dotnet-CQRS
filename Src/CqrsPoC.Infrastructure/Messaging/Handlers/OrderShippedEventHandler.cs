using CqrsPoC.Contracts.Events;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

namespace CqrsPoC.Infrastructure.Messaging.Handlers;

public sealed class OrderShippedEventHandler(ILogger<OrderShippedEventHandler> logger)
    : IHandleMessages<OrderShippedEvent>
{
    public Task Handle(OrderShippedEvent message)
    {
        logger.LogInformation(
            "[EVENT] OrderShipped | Id={OrderId} | At={ShippedAt:O}",
            message.OrderId, message.ShippedAt);

        return Task.CompletedTask;
    }
}
