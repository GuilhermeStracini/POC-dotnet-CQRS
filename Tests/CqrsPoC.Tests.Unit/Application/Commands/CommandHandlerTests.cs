using CqrsPoC.Application.Commands.CancelOrder;
using CqrsPoC.Application.Commands.CompleteOrder;
using CqrsPoC.Application.Commands.ConfirmOrder;
using CqrsPoC.Application.Commands.CreateOrder;
using CqrsPoC.Application.Commands.ShipOrder;
using CqrsPoC.Application.Interfaces;
using CqrsPoC.Contracts.Events;
using CqrsPoC.Domain.Entities;
using CqrsPoC.Domain.Enums;
using CqrsPoC.Domain.Exceptions;
using FluentAssertions;
using Moq;
using Xunit;

namespace CqrsPoC.Tests.Unit.Application.Commands;

/// <summary>
/// Unit tests for all five command handlers.
/// Dependencies (IOrderRepository, IEventPublisher) are mocked with Moq
/// so each test covers only the handler's orchestration logic.
/// </summary>
public sealed class CommandHandlerTests
{
    private readonly Mock<IOrderRepository> _repoMock  = new();
    private readonly Mock<IEventPublisher>  _pubMock   = new();

    // ═════════════════════════════════════════════════════════════════════════
    // CreateOrderCommandHandler
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateOrder_ValidCommand_AddsOrderAndPublishesEvent()
    {
        // Arrange
        Order? saved = null;
        _repoMock.Setup(r => r.AddAsync(It.IsAny<Order>(), default))
                 .Callback<Order, CancellationToken>((o, _) => saved = o)
                 .Returns(Task.CompletedTask);

        var handler = new CreateOrderCommandHandler(_repoMock.Object, _pubMock.Object);
        var command  = new CreateOrderCommand("Alice", "Widget", 100m);

        // Act
        var id = await handler.Handle(command, default);

        // Assert — returned ID matches persisted entity
        id.Should().NotBe(Guid.Empty);
        saved.Should().NotBeNull();
        saved!.Id.Should().Be(id);
        saved.State.Should().Be(OrderState.Pending);

        _repoMock.Verify(r => r.SaveChangesAsync(default), Times.Once);
        _pubMock.Verify(p => p.PublishAsync(
            It.Is<OrderCreatedEvent>(e => e.OrderId == id && e.CustomerName == "Alice"),
            default), Times.Once);
    }

    [Fact]
    public async Task CreateOrder_RepositoryThrows_DoesNotPublishEvent()
    {
        _repoMock.Setup(r => r.SaveChangesAsync(default))
                 .ThrowsAsync(new InvalidOperationException("DB error"));

        var handler = new CreateOrderCommandHandler(_repoMock.Object, _pubMock.Object);

        var act = async () => await handler.Handle(
            new CreateOrderCommand("Alice", "Widget", 50m), default);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _pubMock.Verify(p => p.PublishAsync(It.IsAny<OrderCreatedEvent>(), default), Times.Never);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // ConfirmOrderCommandHandler
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ConfirmOrder_ExistingPendingOrder_ConfirmsAndPublishes()
    {
        var order = BuildPendingOrder();
        SetupRepoGetById(order);

        var handler = new ConfirmOrderCommandHandler(_repoMock.Object, _pubMock.Object);
        await handler.Handle(new ConfirmOrderCommand(order.Id), default);

        order.State.Should().Be(OrderState.Confirmed);
        _repoMock.Verify(r => r.UpdateAsync(order, default), Times.Once);
        _repoMock.Verify(r => r.SaveChangesAsync(default), Times.Once);
        _pubMock.Verify(p => p.PublishAsync(
            It.Is<OrderConfirmedEvent>(e => e.OrderId == order.Id), default), Times.Once);
    }

    [Fact]
    public async Task ConfirmOrder_OrderNotFound_ThrowsOrderNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
                 .ReturnsAsync((Order?)null);

        var handler = new ConfirmOrderCommandHandler(_repoMock.Object, _pubMock.Object);
        var act = async () => await handler.Handle(new ConfirmOrderCommand(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<OrderNotFoundException>();
    }

    [Fact]
    public async Task ConfirmOrder_AlreadyCancelledOrder_ThrowsDomainException()
    {
        var order = BuildPendingOrder();
        order.Cancel("reason");
        SetupRepoGetById(order);

        var handler = new ConfirmOrderCommandHandler(_repoMock.Object, _pubMock.Object);
        var act = async () => await handler.Handle(new ConfirmOrderCommand(order.Id), default);

        await act.Should().ThrowAsync<DomainException>();
        _pubMock.Verify(p => p.PublishAsync(It.IsAny<OrderConfirmedEvent>(), default), Times.Never);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // ShipOrderCommandHandler
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ShipOrder_ConfirmedOrder_ShipsAndPublishes()
    {
        var order = BuildConfirmedOrder();
        SetupRepoGetById(order);

        var handler = new ShipOrderCommandHandler(_repoMock.Object, _pubMock.Object);
        await handler.Handle(new ShipOrderCommand(order.Id), default);

        order.State.Should().Be(OrderState.Shipped);
        _pubMock.Verify(p => p.PublishAsync(
            It.Is<OrderShippedEvent>(e => e.OrderId == order.Id), default), Times.Once);
    }

    [Fact]
    public async Task ShipOrder_PendingOrder_ThrowsDomainException()
    {
        var order = BuildPendingOrder();
        SetupRepoGetById(order);

        var handler = new ShipOrderCommandHandler(_repoMock.Object, _pubMock.Object);
        var act = async () => await handler.Handle(new ShipOrderCommand(order.Id), default);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Ship*Pending*");
    }

    [Fact]
    public async Task ShipOrder_OrderNotFound_ThrowsOrderNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
                 .ReturnsAsync((Order?)null);

        var handler = new ShipOrderCommandHandler(_repoMock.Object, _pubMock.Object);
        var act = async () => await handler.Handle(new ShipOrderCommand(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<OrderNotFoundException>();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CompleteOrderCommandHandler
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CompleteOrder_ShippedOrder_CompletesAndPublishes()
    {
        var order = BuildShippedOrder();
        SetupRepoGetById(order);

        var handler = new CompleteOrderCommandHandler(_repoMock.Object, _pubMock.Object);
        await handler.Handle(new CompleteOrderCommand(order.Id), default);

        order.State.Should().Be(OrderState.Completed);
        _pubMock.Verify(p => p.PublishAsync(
            It.Is<OrderCompletedEvent>(e => e.OrderId == order.Id), default), Times.Once);
    }

    [Fact]
    public async Task CompleteOrder_ConfirmedOrder_ThrowsDomainException()
    {
        var order = BuildConfirmedOrder();
        SetupRepoGetById(order);

        var handler = new CompleteOrderCommandHandler(_repoMock.Object, _pubMock.Object);
        var act = async () => await handler.Handle(new CompleteOrderCommand(order.Id), default);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*Complete*Confirmed*");
    }

    [Fact]
    public async Task CompleteOrder_OrderNotFound_ThrowsOrderNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
                 .ReturnsAsync((Order?)null);

        var handler = new CompleteOrderCommandHandler(_repoMock.Object, _pubMock.Object);
        var act = async () => await handler.Handle(new CompleteOrderCommand(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<OrderNotFoundException>();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CancelOrderCommandHandler
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CancelOrder_PendingOrder_CancelsAndPublishes()
    {
        var order = BuildPendingOrder();
        SetupRepoGetById(order);

        var handler = new CancelOrderCommandHandler(_repoMock.Object, _pubMock.Object);
        await handler.Handle(new CancelOrderCommand(order.Id, "Customer request"), default);

        order.State.Should().Be(OrderState.Cancelled);
        order.CancelReason.Should().Be("Customer request");
        _pubMock.Verify(p => p.PublishAsync(
            It.Is<OrderCancelledEvent>(e =>
                e.OrderId == order.Id && e.Reason == "Customer request"),
            default), Times.Once);
    }

    [Fact]
    public async Task CancelOrder_ConfirmedOrder_CancelsAndPublishes()
    {
        var order = BuildConfirmedOrder();
        SetupRepoGetById(order);

        var handler = new CancelOrderCommandHandler(_repoMock.Object, _pubMock.Object);
        await handler.Handle(new CancelOrderCommand(order.Id, "Out of stock"), default);

        order.State.Should().Be(OrderState.Cancelled);
    }

    [Fact]
    public async Task CancelOrder_ShippedOrder_ThrowsDomainException()
    {
        var order = BuildShippedOrder();
        SetupRepoGetById(order);

        var handler = new CancelOrderCommandHandler(_repoMock.Object, _pubMock.Object);
        var act = async () => await handler.Handle(
            new CancelOrderCommand(order.Id, "Too late"), default);

        await act.Should().ThrowAsync<DomainException>();
        _pubMock.Verify(p => p.PublishAsync(It.IsAny<OrderCancelledEvent>(), default), Times.Never);
    }

    [Fact]
    public async Task CancelOrder_OrderNotFound_ThrowsOrderNotFoundException()
    {
        _repoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), default))
                 .ReturnsAsync((Order?)null);

        var handler = new CancelOrderCommandHandler(_repoMock.Object, _pubMock.Object);
        var act = async () => await handler.Handle(
            new CancelOrderCommand(Guid.NewGuid(), "reason"), default);

        await act.Should().ThrowAsync<OrderNotFoundException>();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private void SetupRepoGetById(Order order) =>
        _repoMock.Setup(r => r.GetByIdAsync(order.Id, default)).ReturnsAsync(order);

    private static Order BuildPendingOrder()    => Order.Create("Test", "Product", 50m);
    private static Order BuildConfirmedOrder()  { var o = BuildPendingOrder(); o.Confirm(); return o; }
    private static Order BuildShippedOrder()   { var o = BuildConfirmedOrder(); o.Ship(); return o; }
}
