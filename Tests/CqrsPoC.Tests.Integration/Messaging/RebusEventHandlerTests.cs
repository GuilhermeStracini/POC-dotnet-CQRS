using CqrsPoC.Contracts.Events;
using CqrsPoC.Infrastructure.Messaging.Handlers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CqrsPoC.Tests.Integration.Messaging;

/// <summary>
/// Integration tests for the five Rebus event handlers.
///
/// Strategy: call IHandleMessages&lt;T&gt;.Handle() directly.
/// The handler contract is just Task Handle(TMessage message), so we do not
/// need Rebus transport wiring to verify handler logic.  Transport delivery
/// guarantees (serialization, routing, retries) belong in a test against a
/// real or containerised broker -- not in a suite that must run without
/// external services.
/// </summary>
public sealed class RebusEventHandlerTests
{
    // -- OrderCreatedEventHandler ---------------------------------------------

    [Fact]
    public async Task OrderCreatedHandler_ValidEvent_CompletesWithoutThrowing()
    {
        var handler = new OrderCreatedEventHandler(NullLogger<OrderCreatedEventHandler>.Instance);

        var @event = new OrderCreatedEvent(
            Guid.NewGuid(),
            "Alice",
            "Widget",
            99.99m,
            DateTime.UtcNow
        );

        var act = async () => await handler.Handle(@event);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OrderCreatedHandler_LogsAtInformationLevel()
    {
        var logger = new CapturingLogger<OrderCreatedEventHandler>();
        var handler = new OrderCreatedEventHandler(logger);
        var @event = new OrderCreatedEvent(Guid.NewGuid(), "Bob", "Gadget", 55m, DateTime.UtcNow);

        await handler.Handle(@event);

        logger
            .Entries.Should()
            .ContainSingle(e =>
                e.Level == LogLevel.Information && e.Message.Contains(@event.OrderId.ToString())
            );
    }

    // -- OrderConfirmedEventHandler -------------------------------------------

    [Fact]
    public async Task OrderConfirmedHandler_ValidEvent_CompletesWithoutThrowing()
    {
        var handler = new OrderConfirmedEventHandler(
            NullLogger<OrderConfirmedEventHandler>.Instance
        );

        var act = async () =>
            await handler.Handle(new OrderConfirmedEvent(Guid.NewGuid(), DateTime.UtcNow));
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OrderConfirmedHandler_LogsAtInformationLevel()
    {
        var logger = new CapturingLogger<OrderConfirmedEventHandler>();
        var handler = new OrderConfirmedEventHandler(logger);
        var id = Guid.NewGuid();

        await handler.Handle(new OrderConfirmedEvent(id, DateTime.UtcNow));

        logger
            .Entries.Should()
            .ContainSingle(e =>
                e.Level == LogLevel.Information && e.Message.Contains(id.ToString())
            );
    }

    // -- OrderShippedEventHandler ---------------------------------------------

    [Fact]
    public async Task OrderShippedHandler_ValidEvent_CompletesWithoutThrowing()
    {
        var handler = new OrderShippedEventHandler(NullLogger<OrderShippedEventHandler>.Instance);

        var act = async () =>
            await handler.Handle(new OrderShippedEvent(Guid.NewGuid(), DateTime.UtcNow));
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OrderShippedHandler_LogsAtInformationLevel()
    {
        var logger = new CapturingLogger<OrderShippedEventHandler>();
        var handler = new OrderShippedEventHandler(logger);
        var id = Guid.NewGuid();

        await handler.Handle(new OrderShippedEvent(id, DateTime.UtcNow));

        logger
            .Entries.Should()
            .ContainSingle(e =>
                e.Level == LogLevel.Information && e.Message.Contains(id.ToString())
            );
    }

    // -- OrderCompletedEventHandler -------------------------------------------

    [Fact]
    public async Task OrderCompletedHandler_ValidEvent_CompletesWithoutThrowing()
    {
        var handler = new OrderCompletedEventHandler(
            NullLogger<OrderCompletedEventHandler>.Instance
        );

        var act = async () =>
            await handler.Handle(new OrderCompletedEvent(Guid.NewGuid(), DateTime.UtcNow));
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OrderCompletedHandler_LogsAtInformationLevel()
    {
        var logger = new CapturingLogger<OrderCompletedEventHandler>();
        var handler = new OrderCompletedEventHandler(logger);
        var id = Guid.NewGuid();

        await handler.Handle(new OrderCompletedEvent(id, DateTime.UtcNow));

        logger
            .Entries.Should()
            .ContainSingle(e =>
                e.Level == LogLevel.Information && e.Message.Contains(id.ToString())
            );
    }

    // -- OrderCancelledEventHandler -------------------------------------------

    [Fact]
    public async Task OrderCancelledHandler_ValidEvent_CompletesWithoutThrowing()
    {
        var handler = new OrderCancelledEventHandler(
            NullLogger<OrderCancelledEventHandler>.Instance
        );

        var act = async () =>
            await handler.Handle(
                new OrderCancelledEvent(Guid.NewGuid(), "No longer needed", DateTime.UtcNow)
            );
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OrderCancelledHandler_LogsAtWarningLevel()
    {
        var logger = new CapturingLogger<OrderCancelledEventHandler>();
        var handler = new OrderCancelledEventHandler(logger);
        var id = Guid.NewGuid();

        await handler.Handle(new OrderCancelledEvent(id, "Out of stock", DateTime.UtcNow));

        logger
            .Entries.Should()
            .ContainSingle(e => e.Level == LogLevel.Warning && e.Message.Contains(id.ToString()));
    }
}

// -- Test helper: captures log entries for assertion -------------------------

internal sealed class LogEntry(LogLevel level, string message)
{
    public LogLevel Level { get; } = level;
    public string Message { get; } = message;
}

internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<LogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => NullLogger.Instance.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        Entries.Add(new LogEntry(logLevel, formatter(state, exception)));
    }
}
