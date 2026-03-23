using CqrsPoC.Contracts.Events;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

namespace CqrsPoC.Infrastructure.Messaging.Handlers;

public sealed class OrderCancelledEventHandler(ILogger<OrderCancelledEventHandler> logger)
    : IHandleMessages<OrderCancelledEvent>
{
    public Task Handle(OrderCancelledEvent message)
    {
        logger.LogWarning(
            "[EVENT] OrderCancelled | Id={OrderId} | Reason={Reason} | At={CancelledAt:O}",
            message.OrderId, message.Reason, message.CancelledAt);

        return Task.CompletedTask;
    }
}
