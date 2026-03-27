namespace CqrsPoC.Application.Interfaces;

/// <summary>
/// Abstraction over Rebus so Application layer stays transport-agnostic.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default)
        where TEvent : class;
}
