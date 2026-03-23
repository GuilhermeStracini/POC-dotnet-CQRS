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

        Task<TestResponse> Next(CancellationToken _) => Task.FromResult(expected);

        var result = await behavior.Handle(request, Next, CancellationToken.None);

        result.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_SuccessfulRequest_LogsInformationTwice()
    {
        var behavior = new LoggingBehavior<TestRequest, TestResponse>(_loggerMock.Object);

        Task<TestResponse> Next(CancellationToken _) => Task.FromResult(new TestResponse("ok"));

        await behavior.Handle(new TestRequest("x"), Next, CancellationToken.None);

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
        Task<TestResponse> Next(CancellationToken _) => throw exception;

        var act = async () => await behavior.Handle(new TestRequest("x"), Next, CancellationToken.None);

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
        Task<TestResponse> Next(CancellationToken _) => Task.Run(async () =>
        {
            await Task.Delay(50); // simulate async work
            return new TestResponse("done");
        });     
        var result = await behavior.Handle(new TestRequest("slow"), Next, CancellationToken.None);

        result.Result.Should().Be("done");
    }
}
