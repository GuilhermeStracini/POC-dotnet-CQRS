using CqrsPoC.Application.Interfaces;
using CqrsPoC.Contracts.Events;
using CqrsPoC.Infrastructure.Messaging;
using CqrsPoC.Infrastructure.Messaging.Handlers;
using CqrsPoC.Infrastructure.Persistence;
using CqrsPoC.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rebus.Config;
using Rebus.Routing.TypeBased;

namespace CqrsPoC.Infrastructure;

public static class DependencyInjection
{
    private const string QueueName = "cqrs-poc-orders";

    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        // ── Persistence (EF Core + InMemory for PoC) ──────────────────────────
        services.AddDbContext<AppDbContext>(opts => opts.UseInMemoryDatabase("CqrsPocDb"));

        services.AddScoped<IOrderRepository, OrderRepository>();

        // ── Messaging (Rebus + RabbitMQ) ──────────────────────────────────────
        var rabbitMqConnectionString =
            configuration.GetConnectionString("RabbitMQ") ?? "amqp://guest:guest@localhost:5672";

        services.AddRebus(configure =>
            configure
                .Transport(t => t.UseRabbitMq(rabbitMqConnectionString, QueueName))
                .Routing(r =>
                    r.TypeBased()
                        .Map<OrderCreatedEvent>(QueueName)
                        .Map<OrderConfirmedEvent>(QueueName)
                        .Map<OrderShippedEvent>(QueueName)
                        .Map<OrderCompletedEvent>(QueueName)
                        .Map<OrderCancelledEvent>(QueueName)
                )
                .Options(o =>
                {
                    o.SetNumberOfWorkers(2);
                    o.SetMaxParallelism(4);
                })
        );

        // ── Register all Rebus message handlers ───────────────────────────────
        services.AddRebusHandler<OrderCreatedEventHandler>();
        services.AddRebusHandler<OrderConfirmedEventHandler>();
        services.AddRebusHandler<OrderShippedEventHandler>();
        services.AddRebusHandler<OrderCompletedEventHandler>();
        services.AddRebusHandler<OrderCancelledEventHandler>();

        // ── Subscribe to all events on startup ────────────────────────────────
        // Done in Program.cs via IServiceProvider after build.

        // ── Event publisher abstraction ───────────────────────────────────────
        services.AddScoped<IEventPublisher, RebusEventPublisher>();

        return services;
    }

    /// <summary>
    /// Subscribes Rebus to all domain events. Must be called after app.Build().
    /// </summary>
    public static async Task SubscribeToEventsAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var bus = scope.ServiceProvider.GetRequiredService<Rebus.Bus.IBus>();

        await bus.Subscribe<OrderCreatedEvent>();
        await bus.Subscribe<OrderConfirmedEvent>();
        await bus.Subscribe<OrderShippedEvent>();
        await bus.Subscribe<OrderCompletedEvent>();
        await bus.Subscribe<OrderCancelledEvent>();
    }
}
