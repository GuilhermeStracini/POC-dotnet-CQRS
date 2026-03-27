namespace CqrsPoC.Contracts.Events;

public record OrderCancelledEvent(Guid OrderId, string Reason, DateTime CancelledAt);
