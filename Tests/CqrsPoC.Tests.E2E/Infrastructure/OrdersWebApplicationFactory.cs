using CqrsPoC.Application.Interfaces;
using CqrsPoC.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Rebus.Bus;
using Rebus.Config;
using Rebus.ServiceProvider;
using Rebus.Transport.InMem;

namespace CqrsPoC.Tests.E2E.Infrastructure;

/// <summary>
/// Spins up the full ASP.NET Core pipeline (real controllers, MediatR, EF Core,
/// exception middleware) but replaces external dependencies so no live services
/// are needed:
///   • EF Core → isolated InMemory database (unique per factory instance)
///   • Rebus   → InMemory transport (no RabbitMQ)
/// </summary>
public sealed class OrdersWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"E2ETestDb_{Guid.NewGuid()}";
    private readonly InMemNetwork _network = new();

    // Expose the publisher mock so tests can assert publish calls
    public Mock<IEventPublisher> PublisherMock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");

        builder.ConfigureServices(services =>
        {
            // ── Replace EF Core with isolated InMemory database ───────────────
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();

            services.AddDbContext<AppDbContext>(opts =>
                opts.UseInMemoryDatabase(_dbName));

            // ── Replace Rebus+RabbitMQ with InMemory transport ────────────────
            // Remove all Rebus registrations from the real Infrastructure DI
            var rebusDescriptors = services
                .Where(d => d.ServiceType.FullName?.Contains("Rebus") == true
                         || d.ImplementationType?.FullName?.Contains("Rebus") == true)
                .ToList();

            foreach (var d in rebusDescriptors)
                services.Remove(d);

            services.AddRebus(cfg => cfg
                .Transport(t => t.UseInMemoryTransport(_network, "e2e-test-queue"))
                .Options(o =>
                {
                    o.SetNumberOfWorkers(1);
                    o.SetMaxParallelism(1);
                }));

            // ── Replace IEventPublisher with a tracked mock ───────────────────
            services.RemoveAll<IEventPublisher>();
            services.AddSingleton(PublisherMock.Object);
        });
    }

    /// <summary>
    /// Seeds an order in the E2E test database and returns its ID.
    /// </summary>
    public async Task<Guid> SeedOrderAsync(
        string customer = "Seed Customer",
        string product  = "Seed Product",
        decimal amount  = 100m)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = CqrsPoC.Domain.Entities.Order.Create(customer, product, amount);
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }
}
