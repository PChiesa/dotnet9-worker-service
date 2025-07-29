using Microsoft.EntityFrameworkCore;
using WorkerService.Domain.Entities;
using WorkerService.Domain.ValueObjects;
using WorkerService.Infrastructure.Data.Configurations;

namespace WorkerService.Infrastructure.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Item> Items => Set<Item>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply entity configurations
        modelBuilder.ApplyConfiguration(new ItemConfiguration());
        modelBuilder.ApplyConfiguration(new OrderItemConfiguration());

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

    }
}