using CqrsPoC.Contracts.Events;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

namespace CqrsPoC.Infrastructure.Messaging.Handlers;

public sealed class OrderCompletedEventHandler(ILogger<OrderCompletedEventHandler> logger)
    : IHandleMessages<OrderCompletedEvent>
{
    public Task Handle(OrderCompletedEvent message)
    {
        logger.LogInformation(
            "[EVENT] OrderCompleted | Id={OrderId} | At={CompletedAt:O}",
            message.OrderId,
            message.CompletedAt
        );

        return Task.CompletedTask;
    }
}
