using CqrsPoC.Domain.Enums;
using CqrsPoC.Domain.Exceptions;
using Stateless;

namespace CqrsPoC.Domain.Entities;

/// <summary>
/// Order aggregate root with an embedded Stateless state machine.
/// The machine enforces valid lifecycle transitions:
///
///   Pending ──[Confirm]──► Confirmed ──[Ship]──► Shipped ──[Complete]──► Completed
///     │                        │
///     └──────[Cancel]──────────┴──────────────────────────────────────► Cancelled
/// </summary>
public class Order
{
    // ── Persisted properties ──────────────────────────────────────────────────
    public Guid Id { get; private set; }
    public string CustomerName { get; private set; } = string.Empty;
    public string ProductName { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public OrderState State { get; private set; }
    public string? CancelReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // ── State machine (not persisted — rebuilt on every hydration) ─────────────
    private StateMachine<OrderState, OrderTrigger> _machine = null!;

    // ── EF Core parameterless constructor ─────────────────────────────────────
    private Order() => BuildMachine();

    // ── Factory constructor ───────────────────────────────────────────────────
    public static Order Create(string customerName, string productName, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(customerName))
            throw new DomainException("Customer name is required.");
        if (string.IsNullOrWhiteSpace(productName))
            throw new DomainException("Product name is required.");
        if (amount <= 0)
            throw new DomainException("Amount must be greater than zero.");

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerName = customerName,
            ProductName = productName,
            Amount = amount,
            State = OrderState.Pending,
            CreatedAt = DateTime.UtcNow,
        };

        order.BuildMachine();
        return order;
    }

    // ── Transition methods ────────────────────────────────────────────────────
    public void Confirm()
    {
        _machine.Fire(OrderTrigger.Confirm);
        Touch();
    }

    public void Ship()
    {
        _machine.Fire(OrderTrigger.Ship);
        Touch();
    }

    public void Complete()
    {
        _machine.Fire(OrderTrigger.Complete);
        Touch();
    }

    public void Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("A cancellation reason must be provided.");

        _machine.Fire(OrderTrigger.Cancel);
        CancelReason = reason;
        Touch();
    }

    // ── Permitted transitions helper (useful for API responses) ───────────────
    public IEnumerable<string> PermittedTriggers =>
        _machine.GetPermittedTriggersAsync().Result.Select(t => t.ToString());

    // ── Private helpers ───────────────────────────────────────────────────────
    private void Touch() => UpdatedAt = DateTime.UtcNow;

    private void BuildMachine()
    {
        // The machine reads/writes State directly on this aggregate.
        _machine = new StateMachine<OrderState, OrderTrigger>(
            stateAccessor: () => State,
            stateMutator: s => State = s
        );

        _machine
            .Configure(OrderState.Pending)
            .Permit(OrderTrigger.Confirm, OrderState.Confirmed)
            .Permit(OrderTrigger.Cancel, OrderState.Cancelled);

        _machine
            .Configure(OrderState.Confirmed)
            .Permit(OrderTrigger.Ship, OrderState.Shipped)
            .Permit(OrderTrigger.Cancel, OrderState.Cancelled);

        _machine.Configure(OrderState.Shipped).Permit(OrderTrigger.Complete, OrderState.Completed);

        // Terminal states — no outgoing transitions
        _machine.Configure(OrderState.Completed);
        _machine.Configure(OrderState.Cancelled);

        // Surface invalid transitions as domain exceptions
        _machine.OnUnhandledTrigger(
            (state, trigger) =>
                throw new DomainException(
                    $"Cannot apply trigger '{trigger}' when order is in state '{state}'."
                )
        );
    }
}
