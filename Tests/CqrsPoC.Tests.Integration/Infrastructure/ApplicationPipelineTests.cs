using CqrsPoC.Application;
using CqrsPoC.Application.Commands.CancelOrder;
using CqrsPoC.Application.Commands.CompleteOrder;
using CqrsPoC.Application.Commands.ConfirmOrder;
using CqrsPoC.Application.Commands.CreateOrder;
using CqrsPoC.Application.Commands.ShipOrder;
using CqrsPoC.Application.Interfaces;
using CqrsPoC.Application.Queries.GetAllOrders;
using CqrsPoC.Application.Queries.GetOrder;
using CqrsPoC.Domain.Enums;
using CqrsPoC.Domain.Exceptions;
using CqrsPoC.Infrastructure.Persistence;
using CqrsPoC.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace CqrsPoC.Tests.Integration.Infrastructure;

/// <summary>
/// Integration tests that wire the real Application layer (MediatR + handlers)
/// with the real EF Core InMemory repository. The only mock is IEventPublisher
/// so we avoid a live RabbitMQ dependency while still verifying publish calls.
/// </summary>
public sealed class ApplicationPipelineTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;
    private readonly Mock<IEventPublisher> _publisherMock = new();

    public ApplicationPipelineTests()
    {
        var services = new ServiceCollection();

        // Real application layer (MediatR + LoggingBehavior)
        services.AddApplication();

        // Real EF Core repo (isolated in-memory database)
        services.AddDbContext<AppDbContext>(opts =>
            opts.UseInMemoryDatabase($"IntegrationDb_{Guid.NewGuid()}")
        );
        services.AddScoped<IOrderRepository, OrderRepository>();

        // Mocked publisher — no RabbitMQ needed
        services.AddSingleton(_publisherMock.Object);

        // Logging (sink-to-null for tests)
        services.AddLogging();

        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    public void Dispose() => _provider.Dispose();

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateOrder_ThenQuery_ReturnsPersistedOrder()
    {
        var id = await _mediator.Send(
            new CreateOrderCommand("Integration Alice", "Int Widget", 49.99m)
        );

        var dto = await _mediator.Send(new GetOrderQuery(id));

        dto.Should().NotBeNull();
        dto.Id.Should().Be(id);
        dto.CustomerName.Should().Be("Integration Alice");
        dto.State.Should().Be(OrderState.Pending);
    }

    // ── Full happy path ───────────────────────────────────────────────────────

    [Fact]
    public async Task FullLifecycle_CreateConfirmShipComplete_StateMachineProgressesCorrectly()
    {
        var id = await _mediator.Send(new CreateOrderCommand("Bob", "Big Order", 1000m));

        await _mediator.Send(new ConfirmOrderCommand(id));
        (await _mediator.Send(new GetOrderQuery(id)))!.State.Should().Be(OrderState.Confirmed);

        await _mediator.Send(new ShipOrderCommand(id));
        (await _mediator.Send(new GetOrderQuery(id)))!.State.Should().Be(OrderState.Shipped);

        await _mediator.Send(new CompleteOrderCommand(id));
        var final = await _mediator.Send(new GetOrderQuery(id));
        final!.State.Should().Be(OrderState.Completed);
        final.PermittedTriggers.Should().BeEmpty();
    }

    [Fact]
    public async Task FullLifecycle_CreateThenCancel_OrderEndsAsCancelled()
    {
        var id = await _mediator.Send(new CreateOrderCommand("Carol", "Cancellable", 99m));

        await _mediator.Send(new CancelOrderCommand(id, "Changed mind"));

        var dto = await _mediator.Send(new GetOrderQuery(id));
        dto!.State.Should().Be(OrderState.Cancelled);
        dto.CancelReason.Should().Be("Changed mind");
    }

    // ── GetAll ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllOrders_AfterCreatingMultiple_ReturnsAllOrders()
    {
        await _mediator.Send(new CreateOrderCommand("A", "Prod1", 10m));
        await _mediator.Send(new CreateOrderCommand("B", "Prod2", 20m));
        await _mediator.Send(new CreateOrderCommand("C", "Prod3", 30m));

        var all = await _mediator.Send(new GetAllOrdersQuery());

        all.Should().HaveCount(3);
    }

    // ── Error paths ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ConfirmOrder_NonExistentId_ThrowsOrderNotFoundException()
    {
        var act = async () => await _mediator.Send(new ConfirmOrderCommand(Guid.NewGuid()));
        await act.Should().ThrowAsync<OrderNotFoundException>();
    }

    [Fact]
    public async Task ShipOrder_PendingOrder_ThrowsDomainException()
    {
        var id = await _mediator.Send(new CreateOrderCommand("Dave", "Prod", 5m));

        var act = async () => await _mediator.Send(new ShipOrderCommand(id));
        await act.Should().ThrowAsync<DomainException>().WithMessage("*Ship*Pending*");
    }

    [Fact]
    public async Task CancelOrder_CompletedOrder_ThrowsDomainException()
    {
        var id = await _mediator.Send(new CreateOrderCommand("Eve", "Prod", 5m));
        await _mediator.Send(new ConfirmOrderCommand(id));
        await _mediator.Send(new ShipOrderCommand(id));
        await _mediator.Send(new CompleteOrderCommand(id));

        var act = async () => await _mediator.Send(new CancelOrderCommand(id, "Can't cancel"));

        await act.Should().ThrowAsync<DomainException>();
    }

    // ── Event publishing ──────────────────────────────────────────────────────

    [Fact]
    public async Task CreateOrder_PublishesOrderCreatedEvent_ExactlyOnce()
    {
        await _mediator.Send(new CreateOrderCommand("Frank", "Prod", 77m));

        _publisherMock.Verify(
            p =>
                p.PublishAsync(
                    It.IsAny<Contracts.Events.OrderCreatedEvent>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task FullLifecycle_PublishesOneEventPerTransition()
    {
        var id = await _mediator.Send(new CreateOrderCommand("Grace", "Prod", 1m));
        await _mediator.Send(new ConfirmOrderCommand(id));
        await _mediator.Send(new ShipOrderCommand(id));
        await _mediator.Send(new CompleteOrderCommand(id));

        _publisherMock.Verify(
            p =>
                p.PublishAsync(
                    It.IsAny<Contracts.Events.OrderCreatedEvent>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        _publisherMock.Verify(
            p =>
                p.PublishAsync(
                    It.IsAny<Contracts.Events.OrderConfirmedEvent>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        _publisherMock.Verify(
            p =>
                p.PublishAsync(
                    It.IsAny<Contracts.Events.OrderShippedEvent>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
        _publisherMock.Verify(
            p =>
                p.PublishAsync(
                    It.IsAny<Contracts.Events.OrderCompletedEvent>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }
}
