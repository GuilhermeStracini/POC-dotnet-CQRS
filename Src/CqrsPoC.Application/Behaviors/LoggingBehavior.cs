using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CqrsPoC.Application.Behaviors;

/// <summary>
/// MediatR pipeline behaviour that logs request entry/exit and measures duration.
/// Placed in the pipeline before the handler, it provides cross-cutting observability
/// for every Command and Query without polluting individual handlers.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger
) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken
    )
    {
        var requestName = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();

        logger.LogInformation(
            "[CQRS] ► Handling {RequestName} | Data: {@Request}",
            requestName,
            request
        );

        try
        {
            var response = await next();
            sw.Stop();

            logger.LogInformation(
                "[CQRS] ✓ Handled  {RequestName} in {Elapsed}ms | Response: {@Response}",
                requestName,
                sw.ElapsedMilliseconds,
                response
            );

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(
                ex,
                "[CQRS] ✗ Failed   {RequestName} in {Elapsed}ms",
                requestName,
                sw.ElapsedMilliseconds
            );
            throw;
        }
    }
}
