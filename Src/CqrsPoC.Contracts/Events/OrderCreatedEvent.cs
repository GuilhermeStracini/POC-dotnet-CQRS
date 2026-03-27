namespace CqrsPoC.Contracts.Events;

public record OrderCreatedEvent(
    Guid OrderId,
    string CustomerName,
    string ProductName,
    decimal Amount,
    DateTime CreatedAt
);
