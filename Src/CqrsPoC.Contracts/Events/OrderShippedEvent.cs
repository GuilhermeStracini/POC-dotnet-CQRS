namespace CqrsPoC.Contracts.Events;

public record OrderShippedEvent(Guid OrderId, DateTime ShippedAt);
