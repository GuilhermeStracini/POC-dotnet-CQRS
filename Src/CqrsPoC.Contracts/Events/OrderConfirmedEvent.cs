namespace CqrsPoC.Contracts.Events;

public record OrderConfirmedEvent(Guid OrderId, DateTime ConfirmedAt);
