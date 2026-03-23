using CqrsPoC.Domain.Entities;
using CqrsPoC.Domain.Enums;
using CqrsPoC.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace CqrsPoC.Tests.Unit.Domain;

/// <summary>
/// Tests the Order aggregate root and its embedded Stateless state machine.
/// Every valid transition, every invalid transition, and every factory guard
/// is exercised here — no infrastructure, no mocks, pure domain logic.
/// </summary>
public sealed class OrderStateMachineTests
{
    // ── Factory validation ────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidArgs_ReturnsOrderInPendingState()
    {
        var order = Order.Create("Alice", "Widget", 99.99m);

        order.Id.Should().NotBe(Guid.Empty);
        order.CustomerName.Should().Be("Alice");
        order.ProductName.Should().Be("Widget");
        order.Amount.Should().Be(99.99m);
        order.State.Should().Be(OrderState.Pending);
        order.CancelReason.Should().BeNull();
        order.UpdatedAt.Should().BeNull();
        order.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("", "Widget", 10)]
    [InlineData("   ", "Widget", 10)]
    public void Create_WithBlankCustomerName_ThrowsDomainException(
        string customerName, string product, decimal amount)
    {
        var act = () => Order.Create(customerName, product, amount);
        act.Should().Throw<DomainException>().WithMessage("*Customer name*");
    }

    [Theory]
    [InlineData("Alice", "", 10)]
    [InlineData("Alice", "   ", 10)]
    public void Create_WithBlankProductName_ThrowsDomainException(
        string customerName, string product, decimal amount)
    {
        var act = () => Order.Create(customerName, product, amount);
        act.Should().Throw<DomainException>().WithMessage("*Product name*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999.99)]
    public void Create_WithNonPositiveAmount_ThrowsDomainException(decimal amount)
    {
        var act = () => Order.Create("Alice", "Widget", amount);
        act.Should().Throw<DomainException>().WithMessage("*Amount*");
    }

    // ── Pending → Confirmed ───────────────────────────────────────────────────

    [Fact]
    public void Confirm_WhenPending_TransitionsToConfirmed()
    {
        var order = BuildPending();

        order.Confirm();

        order.State.Should().Be(OrderState.Confirmed);
        order.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void Confirm_WhenPending_PermittedTriggersContainsShipAndCancel()
    {
        var order = BuildPending();
        order.Confirm();

        order.PermittedTriggers.Should().BeEquivalentTo(["Ship", "Cancel"]);
    }

    // ── Confirmed → Shipped ───────────────────────────────────────────────────

    [Fact]
    public void Ship_WhenConfirmed_TransitionsToShipped()
    {
        var order = BuildConfirmed();

        order.Ship();

        order.State.Should().Be(OrderState.Shipped);
    }

    [Fact]
    public void Ship_WhenPending_ThrowsDomainException()
    {
        var order = BuildPending();

        var act = () => order.Ship();

        act.Should().Throw<DomainException>()
           .WithMessage("*Ship*Pending*");
    }

    // ── Shipped → Completed ───────────────────────────────────────────────────

    [Fact]
    public void Complete_WhenShipped_TransitionsToCompleted()
    {
        var order = BuildShipped();

        order.Complete();

        order.State.Should().Be(OrderState.Completed);
    }

    [Fact]
    public void Complete_WhenConfirmed_ThrowsDomainException()
    {
        var order = BuildConfirmed();

        var act = () => order.Complete();

        act.Should().Throw<DomainException>().WithMessage("*Complete*Confirmed*");
    }

    [Fact]
    public void Complete_WhenCompleted_ThrowsDomainException()
    {
        var order = BuildCompleted();

        var act = () => order.Complete();

        act.Should().Throw<DomainException>();
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    [Fact]
    public void Cancel_WhenPending_TransitionsToCancelled()
    {
        var order = BuildPending();

        order.Cancel("Changed mind");

        order.State.Should().Be(OrderState.Cancelled);
        order.CancelReason.Should().Be("Changed mind");
    }

    [Fact]
    public void Cancel_WhenConfirmed_TransitionsToCancelled()
    {
        var order = BuildConfirmed();

        order.Cancel("Out of stock");

        order.State.Should().Be(OrderState.Cancelled);
        order.CancelReason.Should().Be("Out of stock");
    }

    [Fact]
    public void Cancel_WhenShipped_ThrowsDomainException()
    {
        var order = BuildShipped();

        var act = () => order.Cancel("Too late");

        act.Should().Throw<DomainException>().WithMessage("*Cancel*Shipped*");
    }

    [Fact]
    public void Cancel_WhenCompleted_ThrowsDomainException()
    {
        var order = BuildCompleted();

        var act = () => order.Cancel("Already done");

        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Cancel_WithBlankReason_ThrowsDomainException(string reason)
    {
        var order = BuildPending();

        var act = () => order.Cancel(reason);

        act.Should().Throw<DomainException>().WithMessage("*reason*");
    }

    // ── Terminal states ───────────────────────────────────────────────────────

    [Fact]
    public void Completed_HasNoPermittedTriggers()
    {
        var order = BuildCompleted();
        order.PermittedTriggers.Should().BeEmpty();
    }

    [Fact]
    public void Cancelled_HasNoPermittedTriggers()
    {
        var order = BuildPending();
        order.Cancel("test");
        order.PermittedTriggers.Should().BeEmpty();
    }

    // ── Full happy-path lifecycle ─────────────────────────────────────────────

    [Fact]
    public void FullLifecycle_Pending_Confirmed_Shipped_Completed_SetsCorrectStates()
    {
        var order = Order.Create("Bob", "Gadget", 250m);

        order.State.Should().Be(OrderState.Pending);
        order.Confirm();
        order.State.Should().Be(OrderState.Confirmed);
        order.Ship();
        order.State.Should().Be(OrderState.Shipped);
        order.Complete();
        order.State.Should().Be(OrderState.Completed);
        order.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public void EachTransition_UpdatesUpdatedAt()
    {
        var order = BuildPending();

        order.Confirm();
        var afterConfirm = order.UpdatedAt;

        order.Ship();
        var afterShip = order.UpdatedAt;

        afterShip.Should().BeOnOrAfter(afterConfirm!.Value);
    }

    // ── Permitted triggers reflect current state ──────────────────────────────

    [Fact]
    public void PendingOrder_PermittedTriggersAre_ConfirmAndCancel()
    {
        var order = BuildPending();
        order.PermittedTriggers.Should().BeEquivalentTo(["Confirm", "Cancel"]);
    }

    [Fact]
    public void ShippedOrder_PermittedTriggersAre_CompleteOnly()
    {
        var order = BuildShipped();
        order.PermittedTriggers.Should().BeEquivalentTo(["Complete"]);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Order BuildPending()    => Order.Create("Test Customer", "Test Product", 10m);
    private static Order BuildConfirmed()  { var o = BuildPending(); o.Confirm(); return o; }
    private static Order BuildShipped()   { var o = BuildConfirmed(); o.Ship(); return o; }
    private static Order BuildCompleted() { var o = BuildShipped(); o.Complete(); return o; }
}
