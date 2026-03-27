using CqrsPoC.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CqrsPoC.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);

            entity.Property(o => o.CustomerName).IsRequired().HasMaxLength(200);

            entity.Property(o => o.ProductName).IsRequired().HasMaxLength(200);

            entity.Property(o => o.Amount).HasPrecision(18, 2);

            entity.Property(o => o.State).HasConversion<int>();

            entity.Property(o => o.CancelReason).HasMaxLength(500);

            entity.Property(o => o.CreatedAt).IsRequired();

            entity.Ignore(o => o.PermittedTriggers);
        });
    }
}
