using CqrsPoC.Application.Interfaces;
using CqrsPoC.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CqrsPoC.Infrastructure.Persistence.Repositories;

public sealed class OrderRepository(AppDbContext context) : IOrderRepository
{
    public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Orders.FindAsync([id], ct);

    public async Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken ct = default)
        => await context.Orders
                        .OrderByDescending(o => o.CreatedAt)
                        .ToListAsync(ct);

    public async Task AddAsync(Order order, CancellationToken ct = default)
        => await context.Orders.AddAsync(order, ct);

    public Task UpdateAsync(Order order, CancellationToken ct = default)
    {
        context.Orders.Update(order);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
        => await context.SaveChangesAsync(ct);
}
