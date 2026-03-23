using CqrsPoC.Application.Behaviors;
using Microsoft.Extensions.DependencyInjection;

namespace CqrsPoC.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);

            // Register the logging pipeline behaviour for all requests
            cfg.AddOpenBehavior(typeof(LoggingBehavior<,>));
        });

        return services;
    }
}
