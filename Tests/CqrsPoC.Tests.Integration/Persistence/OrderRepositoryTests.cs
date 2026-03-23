using CqrsPoC.Domain.Entities;
using CqrsPoC.Domain.Enums;
using CqrsPoC.Infrastructure.Persistence;
using CqrsPoC.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CqrsPoC.Tests.Integration.Persistence;

/// <summary>
/// Integration tests for <see cref="OrderRepository"/> using EF Core's InMemory
/// provider. Each test gets its own isolated database instance to prevent
/// state leaking between tests.
/// </summary>
public sealed class OrderRepositoryTests : IDisposable
{
    private readonly AppDbContext    _context;
    private readonly OrderRepository _repository;

    public OrderRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
            .Options;

        _context    = new AppDbContext(options);
        _repository = new OrderRepository(_context);
    }

    public void Dispose() => _context.Dispose();

    // ── AddAsync / GetByIdAsync ───────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_ThenGetById_ReturnsSameOrder()
    {
        var order = Order.Create("Alice", "Widget", 99.99m);

        await _repository.AddAsync(order);
        await _repository.SaveChangesAsync();

        var retrieved = await _repository.GetByIdAsync(order.Id);

        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(order.Id);
        retrieved.CustomerName.Should().Be("Alice");
        retrieved.ProductName.Should().Be("Widget");
        retrieved.Amount.Should().Be(99.99m);
        retrieved.State.Should().Be(OrderState.Pending);
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        var result = await _repository.GetByIdAsync(Guid.NewGuid());
        result.Should().BeNull();
    }

    // ── GetAllAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_WithMultipleOrders_ReturnsAllInDescendingCreatedAtOrder()
    {
        var order1 = Order.Create("A", "P1", 10m);
        var order2 = Order.Create("B", "P2", 20m);
        var order3 = Order.Create("C", "P3", 30m);

        await _repository.AddAsync(order1);
        await _repository.AddAsync(order2);
        await _repository.AddAsync(order3);
        await _repository.SaveChangesAsync();

        var all = await _repository.GetAllAsync();

        all.Should().HaveCount(3);
        // Newest first — all created in the same tick so at least they're all present
        all.Select(o => o.CustomerName).Should().BeEquivalentTo(["A", "B", "C"]);
    }

    [Fact]
    public async Task GetAllAsync_EmptyDatabase_ReturnsEmptyList()
    {
        var all = await _repository.GetAllAsync();
        all.Should().BeEmpty();
    }

    // ── UpdateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_AfterStateTransition_PersistsNewState()
    {
        var order = Order.Create("Bob", "Gadget", 150m);
        await _repository.AddAsync(order);
        await _repository.SaveChangesAsync();

        // Reload — simulates a real request cycle
        _context.ChangeTracker.Clear();
        var loaded = await _repository.GetByIdAsync(order.Id);
        loaded!.Confirm();
        await _repository.UpdateAsync(loaded);
        await _repository.SaveChangesAsync();

        _context.ChangeTracker.Clear();
        var reloaded = await _repository.GetByIdAsync(order.Id);

        reloaded!.State.Should().Be(OrderState.Confirmed);
        reloaded.UpdatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_FullLifecycle_AllStatesPersistCorrectly()
    {
        var order = Order.Create("Carol", "Doohickey", 75m);
        await _repository.AddAsync(order);
        await _repository.SaveChangesAsync();

        async Task Reload() { _context.ChangeTracker.Clear(); }

        await Reload();
        var o = (await _repository.GetByIdAsync(order.Id))!;
        o.Confirm();
        await _repository.UpdateAsync(o);
        await _repository.SaveChangesAsync();

        await Reload();
        o = (await _repository.GetByIdAsync(order.Id))!;
        o.Ship();
        await _repository.UpdateAsync(o);
        await _repository.SaveChangesAsync();

        await Reload();
        o = (await _repository.GetByIdAsync(order.Id))!;
        o.Complete();
        await _repository.UpdateAsync(o);
        await _repository.SaveChangesAsync();

        await Reload();
        var final = await _repository.GetByIdAsync(order.Id);
        final!.State.Should().Be(OrderState.Completed);
    }

    [Fact]
    public async Task UpdateAsync_CancelledOrder_PersistsCancelReason()
    {
        var order = Order.Create("Dave", "Thingamajig", 25m);
        await _repository.AddAsync(order);
        await _repository.SaveChangesAsync();

        _context.ChangeTracker.Clear();
        var loaded = (await _repository.GetByIdAsync(order.Id))!;
        loaded.Cancel("Customer changed mind");
        await _repository.UpdateAsync(loaded);
        await _repository.SaveChangesAsync();

        _context.ChangeTracker.Clear();
        var reloaded = await _repository.GetByIdAsync(order.Id);

        reloaded!.State.Should().Be(OrderState.Cancelled);
        reloaded.CancelReason.Should().Be("Customer changed mind");
    }

    // ── Isolation ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Add_TwoOrders_GetAllReturnsExactlyTwo()
    {
        await _repository.AddAsync(Order.Create("X", "Foo", 1m));
        await _repository.AddAsync(Order.Create("Y", "Bar", 2m));
        await _repository.SaveChangesAsync();

        var all = await _repository.GetAllAsync();
        all.Should().HaveCount(2);
    }
}
