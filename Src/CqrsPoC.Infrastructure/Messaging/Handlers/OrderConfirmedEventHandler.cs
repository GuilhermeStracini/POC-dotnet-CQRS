using CqrsPoC.Contracts.Events;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

namespace CqrsPoC.Infrastructure.Messaging.Handlers;

public sealed class OrderConfirmedEventHandler(ILogger<OrderConfirmedEventHandler> logger)
    : IHandleMessages<OrderConfirmedEvent>
{
    public Task Handle(OrderConfirmedEvent message)
    {
        logger.LogInformation(
            "[EVENT] OrderConfirmed | Id={OrderId} | At={ConfirmedAt:O}",
            message.OrderId,
            message.ConfirmedAt
        );

        return Task.CompletedTask;
    }
}
