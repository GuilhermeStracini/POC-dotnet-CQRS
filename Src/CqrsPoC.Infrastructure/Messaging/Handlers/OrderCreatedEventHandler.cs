using CqrsPoC.Contracts.Events;
using Microsoft.Extensions.Logging;
using Rebus.Handlers;

namespace CqrsPoC.Infrastructure.Messaging.Handlers;

/// <summary>
/// Rebus subscriber for <see cref="OrderCreatedEvent"/>.
/// In a real system this might trigger an email notification, update a read-model,
/// start a payment workflow, etc. Here it logs to show the full Rebus roundtrip.
/// </summary>
public sealed class OrderCreatedEventHandler(ILogger<OrderCreatedEventHandler> logger)
    : IHandleMessages<OrderCreatedEvent>
{
    public Task Handle(OrderCreatedEvent message)
    {
        logger.LogInformation(
            "[EVENT] OrderCreated | Id={OrderId} | Customer={Customer} | Product={Product} | Amount={Amount:C}",
            message.OrderId, message.CustomerName, message.ProductName, message.Amount);

        return Task.CompletedTask;
    }
}
