using CqrsPoC.Contracts.Events;
using CqrsPoC.Infrastructure.Messaging.Handlers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Rebus.Activation;
using Rebus.Bus;
using Rebus.Config;
using Rebus.Routing.TypeBased;
using Rebus.Transport.InMem;
using Stateless.Graph;
using Xunit;

namespace CqrsPoC.Tests.Integration.Messaging;

/// <summary>
/// Integration tests for the Rebus event handlers using the in-memory transport.
/// These verify that each handler correctly receives a published event and
/// processes it without errors — no live RabbitMQ required.
/// </summary>
public sealed class RebusEventHandlerTests : IAsyncDisposable
{
    private const string QueueName = "test-orders-queue";

    private readonly BuiltinHandlerActivator _activator;
    private readonly IBusStarter _starter;
    private readonly IBus _bus;
    private readonly InMemNetwork _network;

    public RebusEventHandlerTests()
    {
        _network = new InMemNetwork();
        _activator = new BuiltinHandlerActivator();

        _starter = Configure
            .With(_activator)
            .Transport(t => t.UseInMemoryTransport(_network, QueueName))
            .Routing(r =>
                r.TypeBased()
                    .Map<OrderCreatedEvent>(QueueName)
                    .Map<OrderConfirmedEvent>(QueueName)
                    .Map<OrderShippedEvent>(QueueName)
                    .Map<OrderCompletedEvent>(QueueName)
                    .Map<OrderCancelledEvent>(QueueName)
            )
            .Options(o => o.SetNumberOfWorkers(0))
            .Create();

        _bus = _starter.Bus;
    }

    public async ValueTask DisposeAsync()
    {
        _bus.Dispose();
        _activator.Dispose();
        await Task.CompletedTask;
    }

    // ── OrderCreated ──────────────────────────────────────────────────────────

    [Fact]
    public async Task OrderCreatedHandler_ReceivesPublishedEvent_WithoutThrowing()
    {
        var received = new TaskCompletionSource<OrderCreatedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        var handler = new OrderCreatedEventHandler(NullLogger<OrderCreatedEventHandler>.Instance);

        _activator.Handle<OrderCreatedEvent>(async msg =>
        {
            await handler.Handle(msg);
            received.SetResult(msg);
        });

        _starter.Start();

        var @event = new OrderCreatedEvent(
            Guid.NewGuid(),
            "Alice",
            "Widget",
            99.99m,
            DateTime.UtcNow
        );

        await _bus.Send(@event);

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

        result.OrderId.Should().Be(@event.OrderId);
        result.CustomerName.Should().Be("Alice");
    }

    // ── OrderConfirmed ────────────────────────────────────────────────────────

    [Fact]
    public async Task OrderConfirmedHandler_ReceivesPublishedEvent_WithoutThrowing()
    {
        var received = new TaskCompletionSource<OrderConfirmedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        var handler = new OrderConfirmedEventHandler(
            NullLogger<OrderConfirmedEventHandler>.Instance
        );

        _activator.Handle<OrderConfirmedEvent>(async msg =>
        {
            await handler.Handle(msg);
            received.SetResult(msg);
        });
        _starter.Start();

        var @event = new OrderConfirmedEvent(Guid.NewGuid(), DateTime.UtcNow);
        await _bus.Send(@event);

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.Should().NotBeNull();
    }

    // ── OrderShipped ──────────────────────────────────────────────────────────

    [Fact]
    public async Task OrderShippedHandler_ReceivesPublishedEvent_WithoutThrowing()
    {
        var received = new TaskCompletionSource<OrderShippedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        var handler = new OrderShippedEventHandler(NullLogger<OrderShippedEventHandler>.Instance);

        _activator.Handle<OrderShippedEvent>(async msg =>
        {
            await handler.Handle(msg);
            received.SetResult(msg);
        });
        _starter.Start();

        await _bus.Send(new OrderShippedEvent(Guid.NewGuid(), DateTime.UtcNow));
        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.Should().NotBeNull();
    }

    // ── OrderCompleted ────────────────────────────────────────────────────────

    [Fact]
    public async Task OrderCompletedHandler_ReceivesPublishedEvent_WithoutThrowing()
    {
        var received = new TaskCompletionSource<OrderCompletedEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        var handler = new OrderCompletedEventHandler(
            NullLogger<OrderCompletedEventHandler>.Instance
        );

        _activator.Handle<OrderCompletedEvent>(async msg =>
        {
            await handler.Handle(msg);
            received.SetResult(msg);
        });
        _starter.Start();

        await _bus.Send(new OrderCompletedEvent(Guid.NewGuid(), DateTime.UtcNow));
        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.Should().NotBeNull();
    }

    // ── OrderCancelled ────────────────────────────────────────────────────────

    [Fact]
    public async Task OrderCancelledHandler_ReceivesPublishedEvent_WithoutThrowing()
    {
        var received = new TaskCompletionSource<OrderCancelledEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );

        var handler = new OrderCancelledEventHandler(
            NullLogger<OrderCancelledEventHandler>.Instance
        );

        _activator.Handle<OrderCancelledEvent>(async msg =>
        {
            await handler.Handle(msg);
            received.SetResult(msg);
        });
        _starter.Start();

        var @event = new OrderCancelledEvent(Guid.NewGuid(), "Test reason", DateTime.UtcNow);
        await _bus.Send(@event);

        var result = await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.Reason.Should().Be("Test reason");
    }
}
