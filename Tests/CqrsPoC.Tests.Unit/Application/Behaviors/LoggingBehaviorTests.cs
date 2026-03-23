using CqrsPoC.Application.Behaviors;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CqrsPoC.Tests.Unit.Application.Behaviors;

public sealed class LoggingBehaviorTests
{
    private readonly Mock<ILogger<LoggingBehavior<TestRequest, TestResponse>>> _loggerMock = new();

    // ─────────────────────────────────────────────────────────────────────────
    // Test doubles
    // ─────────────────────────────────────────────────────────────────────────
    public record TestRequest(string Value) : IRequest<TestResponse>;
    public record TestResponse(string Result);

    // ─────────────────────────────────────────────────────────────────────────
    // Tests
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_SuccessfulRequest_CallsNextAndReturnsResponse()
    {
        var behavior = new LoggingBehavior<TestRequest, TestResponse>(_loggerMock.Object);
        var request  = new TestRequest("hello");
        var expected = new TestResponse("world");

        RequestHandlerDelegate<TestResponse> next = () => Task.FromResult(expected);

        var result = await behavior.Handle(request, next, default);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_SuccessfulRequest_LogsInformationTwice()
    {
        var behavior = new LoggingBehavior<TestRequest, TestResponse>(_loggerMock.Object);
        RequestHandlerDelegate<TestResponse> next =
            () => Task.FromResult(new TestResponse("ok"));

        await behavior.Handle(new TestRequest("x"), next, default);

        // Expect one entry log and one exit log (both Information)
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_ThrowingNext_LogsErrorAndRethrows()
    {
        var behavior  = new LoggingBehavior<TestRequest, TestResponse>(_loggerMock.Object);
        var exception = new InvalidOperationException("boom");
        RequestHandlerDelegate<TestResponse> next = () => throw exception;

        var act = async () => await behavior.Handle(new TestRequest("x"), next, default);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                exception,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_SlowRequest_StillReturnsCorrectResponse()
    {
        var behavior = new LoggingBehavior<TestRequest, TestResponse>(_loggerMock.Object);
        RequestHandlerDelegate<TestResponse> next = async () =>
        {
            await Task.Delay(50); // simulate async work
            return new TestResponse("done");
        };

        var result = await behavior.Handle(new TestRequest("slow"), next, default);

        result.Result.Should().Be("done");
    }
}
