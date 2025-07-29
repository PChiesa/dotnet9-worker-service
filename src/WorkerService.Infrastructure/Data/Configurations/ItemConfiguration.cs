using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkerService.Domain.Entities;
using WorkerService.Domain.ValueObjects;

namespace WorkerService.Infrastructure.Data.Configurations;

public class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("Items");
        
        builder.HasKey(i => i.Id);
        
        builder.Property(i => i.Id)
            .ValueGeneratedNever();
        
        // Configure SKU as owned type
        builder.OwnsOne(i => i.SKU, sku =>
        {
            sku.Property(s => s.Value)
                .HasColumnName("SKU")
                .HasMaxLength(50)
                .IsRequired();
            
            sku.HasIndex(s => s.Value)
                .IsUnique()
                .HasDatabaseName("IX_Items_SKU");
        });
        
        builder.Property(i => i.Name)
            .HasMaxLength(200)
            .IsRequired();
        
        builder.Property(i => i.Description)
            .HasMaxLength(1000);
        
        // Configure Price as owned type
        builder.OwnsOne(i => i.Price, price =>
        {
            price.Property(p => p.Amount)
                .HasColumnName("Price")
                .HasPrecision(10, 2)
                .IsRequired();
            
            price.Property(p => p.Currency)
                .HasColumnName("Currency")
                .HasMaxLength(3)
                .HasDefaultValue("USD")
                .IsRequired();
        });
        
        // Configure StockLevel as owned type
        builder.OwnsOne(i => i.StockLevel, stock =>
        {
            stock.Property(s => s.Available)
                .HasColumnName("AvailableStock")
                .IsRequired();
            
            stock.Property(s => s.Reserved)
                .HasColumnName("ReservedStock")
                .IsRequired();
        });
        
        builder.Property(i => i.Category)
            .HasMaxLength(100)
            .IsRequired();
        
        builder.Property(i => i.IsActive)
            .IsRequired();
        
        builder.Property(i => i.CreatedAt)
            .IsRequired();
        
        builder.Property(i => i.UpdatedAt)
            .IsRequired();
        
        // Configure optimistic concurrency
        builder.Property(i => i.Version)
            .IsConcurrencyToken()
            .ValueGeneratedOnAddOrUpdate();
        
        // Indexes
        builder.HasIndex(i => i.Category)
            .HasDatabaseName("IX_Items_Category");
        
        builder.HasIndex(i => i.IsActive)
            .HasDatabaseName("IX_Items_IsActive");
        
        builder.HasIndex(i => new { i.Category, i.IsActive })
            .HasDatabaseName("IX_Items_Category_IsActive");
        
        // Ignore domain events
        builder.Ignore(i => i.DomainEvents);
    }
}