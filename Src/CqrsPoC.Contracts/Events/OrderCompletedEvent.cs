namespace CqrsPoC.Contracts.Events;

public record OrderCompletedEvent(Guid OrderId, DateTime CompletedAt);
