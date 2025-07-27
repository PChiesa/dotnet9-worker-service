using Microsoft.EntityFrameworkCore;
using WorkerService.Domain.Entities;
using WorkerService.Domain.ValueObjects;

namespace WorkerService.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Order entity
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.Property(e => e.CustomerId)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.OrderDate)
                .IsRequired();

            entity.Property(e => e.Status)
                .IsRequired()
                .HasConversion<string>();

            entity.OwnsOne(e => e.TotalAmount, money =>
            {
                money.Property(m => m.Amount)
                    .HasColumnName("TotalAmount")
                    .HasPrecision(18, 2)
                    .IsRequired();
            });

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.Property(e => e.UpdatedAt)
                .IsRequired();

            // Configure one-to-many relationship with OrderItems
            entity.HasMany(e => e.Items)
                .WithOne()
                .HasForeignKey("OrderId")
                .IsRequired();

            // Ignore domain events (they should not be persisted)
            entity.Ignore(e => e.DomainEvents);

            // Configure indexes
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.OrderDate);
        });

        // Configure OrderItem entity
        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ProductId)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(e => e.Quantity)
                .IsRequired();

            entity.OwnsOne(e => e.UnitPrice, money =>
            {
                money.Property(m => m.Amount)
                    .HasColumnName("UnitPrice")
                    .HasPrecision(18, 2)
                    .IsRequired();
            });

            // Configure indexes
            entity.HasIndex(e => e.ProductId);
        });
    }
}