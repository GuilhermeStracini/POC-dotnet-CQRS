using CqrsPoC.Application.Interfaces;
using CqrsPoC.Application.Queries.GetAllOrders;
using CqrsPoC.Application.Queries.GetOrder;
using CqrsPoC.Domain.Entities;
using CqrsPoC.Domain.Enums;
using FluentAssertions;
using Moq;
using Xunit;

namespace CqrsPoC.Tests.Unit.Application.Queries;

public sealed class QueryHandlerTests
{
    private readonly Mock<IOrderRepository> _repoMock = new();

    // ═════════════════════════════════════════════════════════════════════════
    // GetOrderQueryHandler
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetOrder_ExistingId_ReturnsCorrectDto()
    {
        var order = Order.Create("Alice", "Widget", 99.99m);
        _repoMock.Setup(r => r.GetByIdAsync(order.Id, CancellationToken.None)).ReturnsAsync(order);

        var handler = new GetOrderQueryHandler(_repoMock.Object);
        var result = await handler.Handle(new GetOrderQuery(order.Id), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Id.Should().Be(order.Id);
        result.CustomerName.Should().Be("Alice");
        result.ProductName.Should().Be("Widget");
        result.Amount.Should().Be(99.99m);
        result.State.Should().Be(OrderState.Pending);
        result.StateName.Should().Be("Pending");
        result.CancelReason.Should().BeNull();
        result.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public async Task GetOrder_NonExistingId_ReturnsNull()
    {
        _repoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), CancellationToken.None))
            .ReturnsAsync((Order?)null);

        var handler = new GetOrderQueryHandler(_repoMock.Object);
        var result = await handler.Handle(
            new GetOrderQuery(Guid.NewGuid()),
            CancellationToken.None
        );

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOrder_ConfirmedOrder_ReturnsCorrectStateAndPermittedTriggers()
    {
        var order = Order.Create("Bob", "Gadget", 200m);
        order.Confirm();
        _repoMock.Setup(r => r.GetByIdAsync(order.Id, CancellationToken.None)).ReturnsAsync(order);

        var handler = new GetOrderQueryHandler(_repoMock.Object);
        var result = await handler.Handle(new GetOrderQuery(order.Id), CancellationToken.None);

        result!.State.Should().Be(OrderState.Confirmed);
        result.StateName.Should().Be("Confirmed");
        result.PermittedTriggers.Should().BeEquivalentTo(["Ship", "Cancel"]);
        result.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetOrder_CancelledOrder_ReturnsCancelReasonAndNoTriggers()
    {
        var order = Order.Create("Carol", "Doohickey", 50m);
        order.Cancel("No longer needed");
        _repoMock.Setup(r => r.GetByIdAsync(order.Id, CancellationToken.None)).ReturnsAsync(order);

        var handler = new GetOrderQueryHandler(_repoMock.Object);
        var result = await handler.Handle(new GetOrderQuery(order.Id), CancellationToken.None);

        result!.CancelReason.Should().Be("No longer needed");
        result.PermittedTriggers.Should().BeEmpty();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // GetAllOrdersQueryHandler
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetAllOrders_MultipleOrders_ReturnsAllMappedCorrectly()
    {
        var orders = new List<Order>
        {
            Order.Create("Alice", "Widget A", 10m),
            Order.Create("Bob", "Widget B", 20m),
            Order.Create("Carol", "Widget C", 30m),
        };

        _repoMock
            .Setup(r => r.GetAllAsync(CancellationToken.None))
            .ReturnsAsync(orders.AsReadOnly());

        var handler = new GetAllOrdersQueryHandler(_repoMock.Object);
        var results = await handler.Handle(new GetAllOrdersQuery(), CancellationToken.None);

        results.Should().HaveCount(3);
        results.Select(r => r.CustomerName).Should().BeEquivalentTo(["Alice", "Bob", "Carol"]);
    }

    [Fact]
    public async Task GetAllOrders_EmptyRepository_ReturnsEmptyList()
    {
        _repoMock
            .Setup(r => r.GetAllAsync(CancellationToken.None))
            .ReturnsAsync(new List<Order>().AsReadOnly());

        var handler = new GetAllOrdersQueryHandler(_repoMock.Object);
        var results = await handler.Handle(new GetAllOrdersQuery(), CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllOrders_MixedStates_EachDtoReflectsItsOwnState()
    {
        var pending = Order.Create("P", "Prod", 1m);
        var confirmed = Order.Create("C", "Prod", 2m);
        confirmed.Confirm();
        var shipped = Order.Create("S", "Prod", 3m);
        shipped.Confirm();
        shipped.Ship();

        _repoMock
            .Setup(r => r.GetAllAsync(CancellationToken.None))
            .ReturnsAsync(new List<Order> { pending, confirmed, shipped }.AsReadOnly());

        var handler = new GetAllOrdersQueryHandler(_repoMock.Object);
        var results = await handler.Handle(new GetAllOrdersQuery(), CancellationToken.None);

        results.Should().ContainSingle(r => r.State == OrderState.Pending);
        results.Should().ContainSingle(r => r.State == OrderState.Confirmed);
        results.Should().ContainSingle(r => r.State == OrderState.Shipped);
    }
}
