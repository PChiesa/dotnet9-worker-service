using FluentAssertions;
using WorkerService.Domain.Entities;
using WorkerService.Domain.Events;
using WorkerService.Domain.ValueObjects;
using Xunit;

namespace WorkerService.UnitTests.Domain;

public class ItemTests
{
    [Fact]
    public void Item_Creation_Should_Set_Properties_Correctly()
    {
        // Arrange
        var sku = new SKU("TEST-001");
        var name = "Test Product";
        var description = "Test Description";
        var price = new Price(25.99m);
        var initialStock = 100;
        var category = "Electronics";

        // Act
        var item = new Item(sku, name, description, price, initialStock, category);

        // Assert
        item.Id.Should().NotBeEmpty();
        item.SKU.Should().Be(sku);
        item.Name.Should().Be(name);
        item.Description.Should().Be(description);
        item.Price.Should().Be(price);
        item.StockLevel.Available.Should().Be(initialStock);
        item.StockLevel.Reserved.Should().Be(0);
        item.Category.Should().Be(category);
        item.IsActive.Should().BeTrue();
        item.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        item.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        item.Version.Should().NotBeNull();
        item.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<ItemCreatedEvent>();
    }

    [Fact]
    public void Item_Creation_With_Null_SKU_Should_Throw_Exception()
    {
        // Arrange
        SKU sku = null!;
        var name = "Test Product";
        var description = "Test Description";
        var price = new Price(25.99m);
        var initialStock = 100;
        var category = "Electronics";

        // Act & Assert
        var action = () => new Item(sku, name, description, price, initialStock, category);
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName(nameof(sku));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]    
    public void Item_Creation_With_Invalid_Name_Should_Throw_Exception(string invalidName)
    {
        // Arrange
        var sku = new SKU("TEST-001");
        var description = "Test Description";
        var price = new Price(25.99m);
        var initialStock = 100;
        var category = "Electronics";

        // Act & Assert
        var action = () => new Item(sku, invalidName, description, price, initialStock, category);
        action.Should().Throw<ArgumentException>()
            .WithMessage("Item name cannot be empty*");
    }

    [Fact]
    public void Item_Creation_With_Long_Name_Should_Throw_Exception()
    {
        // Arrange
        var sku = new SKU("TEST-001");
        var longName = new string('A', 201); // Exceeds 200 character limit
        var description = "Test Description";
        var price = new Price(25.99m);
        var initialStock = 100;
        var category = "Electronics";

        // Act & Assert
        var action = () => new Item(sku, longName, description, price, initialStock, category);
        action.Should().Throw<ArgumentException>()
            .WithMessage("Item name cannot exceed 200 characters*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]    
    public void Item_Creation_With_Invalid_Category_Should_Throw_Exception(string invalidCategory)
    {
        // Arrange
        var sku = new SKU("TEST-001");
        var name = "Test Product";
        var description = "Test Description";
        var price = new Price(25.99m);
        var initialStock = 100;

        // Act & Assert
        var action = () => new Item(sku, name, description, price, initialStock, invalidCategory);
        action.Should().Throw<ArgumentException>()
            .WithMessage("Category cannot be empty*");
    }    

    [Fact]
    public void Update_Should_Modify_Properties_And_Raise_Event()
    {
        // Arrange
        var item = CreateTestItem();
        var originalUpdatedAt = item.UpdatedAt;
        var originalVersion = item.Version;
        var newName = "Updated Product";
        var newDescription = "Updated Description";
        var newPrice = new Price(35.99m);
        var newCategory = "Updated Category";

        // Act
        Thread.Sleep(1); // Ensure time passes
        item.Update(newName, newDescription, newPrice, newCategory);

        // Assert
        item.Name.Should().Be(newName);
        item.Description.Should().Be(newDescription);
        item.Price.Should().Be(newPrice);
        item.Category.Should().Be(newCategory);
        item.UpdatedAt.Should().BeAfter(originalUpdatedAt);
        item.Version.Should().NotEqual(originalVersion);
        item.DomainEvents.Should().HaveCount(2); // ItemCreated + ItemUpdated
        item.DomainEvents.Last().Should().BeOfType<ItemUpdatedEvent>();
    }

    [Fact]
    public void Update_Inactive_Item_Should_Throw_Exception()
    {
        // Arrange
        var item = CreateTestItem();
        item.Deactivate();

        // Act & Assert
        var action = () => item.Update("New Name", "New Description", new Price(30.99m), "New Category");
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot update inactive item");
    }

    [Fact]
    public void Update_With_Same_Values_Should_Not_Change_UpdatedAt_Or_Version()
    {
        // Arrange
        var item = CreateTestItem();
        var originalUpdatedAt = item.UpdatedAt;
        var originalVersion = item.Version;
        var originalEventCount = item.DomainEvents.Count;

        // Act
        item.Update(item.Name, item.Description, item.Price, item.Category);

        // Assert
        item.UpdatedAt.Should().Be(originalUpdatedAt);
        item.Version.Should().Equal(originalVersion);
        item.DomainEvents.Should().HaveCount(originalEventCount); // No new events
    }

    [Fact]
    public void AdjustStock_Should_Update_Stock_And_Raise_Event()
    {
        // Arrange
        var item = CreateTestItem();
        var originalUpdatedAt = item.UpdatedAt;
        var originalVersion = item.Version;
        var newQuantity = 150;

        // Act
        Thread.Sleep(1); // Ensure time passes
        item.AdjustStock(newQuantity);

        // Assert
        item.StockLevel.Available.Should().Be(newQuantity);
        item.UpdatedAt.Should().BeAfter(originalUpdatedAt);
        item.Version.Should().NotEqual(originalVersion);
        item.DomainEvents.Should().HaveCount(2); // ItemCreated + StockAdjusted
        item.DomainEvents.Last().Should().BeOfType<StockAdjustedEvent>();
    }

    [Fact]
    public void AdjustStock_On_Inactive_Item_Should_Throw_Exception()
    {
        // Arrange
        var item = CreateTestItem();
        item.Deactivate();

        // Act & Assert
        var action = () => item.AdjustStock(150);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot adjust stock for inactive item");
    }

    [Fact]
    public void ReserveStock_Should_Reserve_Stock_And_Raise_Event()
    {
        // Arrange
        var item = CreateTestItem();
        var originalAvailable = item.StockLevel.Available;
        var originalReserved = item.StockLevel.Reserved;
        var reserveQuantity = 25;

        // Act
        item.ReserveStock(reserveQuantity);

        // Assert
        item.StockLevel.Available.Should().Be(originalAvailable - reserveQuantity);
        item.StockLevel.Reserved.Should().Be(originalReserved + reserveQuantity);
        item.DomainEvents.Should().HaveCount(2); // ItemCreated + StockReserved
        item.DomainEvents.Last().Should().BeOfType<StockReservedEvent>();
    }

    [Fact]
    public void ReserveStock_On_Inactive_Item_Should_Throw_Exception()
    {
        // Arrange
        var item = CreateTestItem();
        item.Deactivate();

        // Act & Assert
        var action = () => item.ReserveStock(25);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot reserve stock for inactive item");
    }

    [Fact]
    public void ReleaseStock_Should_Release_Reserved_Stock_And_Raise_Event()
    {
        // Arrange
        var item = CreateTestItem();
        item.ReserveStock(25);
        var originalAvailable = item.StockLevel.Available;
        var originalReserved = item.StockLevel.Reserved;
        var releaseQuantity = 10;

        // Act
        item.ReleaseStock(releaseQuantity);

        // Assert
        item.StockLevel.Available.Should().Be(originalAvailable + releaseQuantity);
        item.StockLevel.Reserved.Should().Be(originalReserved - releaseQuantity);
        item.DomainEvents.Should().HaveCount(3); // ItemCreated + StockReserved + StockReleased
        item.DomainEvents.Last().Should().BeOfType<StockReleasedEvent>();
    }

    [Fact]
    public void CommitStock_Should_Commit_Reserved_Stock_And_Raise_Event()
    {
        // Arrange
        var item = CreateTestItem();
        item.ReserveStock(25);
        var originalAvailable = item.StockLevel.Available;
        var originalReserved = item.StockLevel.Reserved;
        var commitQuantity = 10;

        // Act
        item.CommitStock(commitQuantity);

        // Assert
        item.StockLevel.Available.Should().Be(originalAvailable);
        item.StockLevel.Reserved.Should().Be(originalReserved - commitQuantity);
        item.DomainEvents.Should().HaveCount(3); // ItemCreated + StockReserved + StockCommitted
        item.DomainEvents.Last().Should().BeOfType<StockCommittedEvent>();
    }

    [Fact]
    public void Deactivate_Should_Set_IsActive_False_And_Raise_Event()
    {
        // Arrange
        var item = CreateTestItem();
        var originalUpdatedAt = item.UpdatedAt;
        var originalVersion = item.Version;

        // Act
        Thread.Sleep(1); // Ensure time passes
        item.Deactivate();

        // Assert
        item.IsActive.Should().BeFalse();
        item.UpdatedAt.Should().BeAfter(originalUpdatedAt);
        item.Version.Should().NotEqual(originalVersion);
        item.DomainEvents.Should().HaveCount(2); // ItemCreated + ItemDeactivated
        item.DomainEvents.Last().Should().BeOfType<ItemDeactivatedEvent>();
    }

    [Fact]
    public void Deactivate_Already_Inactive_Item_Should_Not_Change_State()
    {
        // Arrange
        var item = CreateTestItem();
        item.Deactivate();
        var updatedAt = item.UpdatedAt;
        var version = item.Version;
        var eventCount = item.DomainEvents.Count;

        // Act
        item.Deactivate();

        // Assert
        item.IsActive.Should().BeFalse();
        item.UpdatedAt.Should().Be(updatedAt);
        item.Version.Should().Equal(version);
        item.DomainEvents.Should().HaveCount(eventCount); // No new events
    }

    [Fact]
    public void Activate_Should_Set_IsActive_True_And_Raise_Event()
    {
        // Arrange
        var item = CreateTestItem();
        item.Deactivate();
        var originalUpdatedAt = item.UpdatedAt;
        var originalVersion = item.Version;

        // Act
        Thread.Sleep(1); // Ensure time passes
        item.Activate();

        // Assert
        item.IsActive.Should().BeTrue();
        item.UpdatedAt.Should().BeAfter(originalUpdatedAt);
        item.Version.Should().NotEqual(originalVersion);
        item.DomainEvents.Should().HaveCount(3); // ItemCreated + ItemDeactivated + ItemActivated
        item.DomainEvents.Last().Should().BeOfType<ItemActivatedEvent>();
    }

    [Fact]
    public void Activate_Already_Active_Item_Should_Not_Change_State()
    {
        // Arrange
        var item = CreateTestItem();
        var updatedAt = item.UpdatedAt;
        var version = item.Version;
        var eventCount = item.DomainEvents.Count;

        // Act
        item.Activate();

        // Assert
        item.IsActive.Should().BeTrue();
        item.UpdatedAt.Should().Be(updatedAt);
        item.Version.Should().Equal(version);
        item.DomainEvents.Should().HaveCount(eventCount); // No new events
    }

    [Fact]
    public void ClearDomainEvents_Should_Remove_All_Events()
    {
        // Arrange
        var item = CreateTestItem();
        item.Update("New Name", "New Description", new Price(30.99m), "New Category");
        item.DomainEvents.Should().HaveCount(2); // ItemCreated + ItemUpdated

        // Act
        item.ClearDomainEvents();

        // Assert
        item.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Item_Properties_Should_Be_Set_Correctly_On_Creation()
    {
        // Arrange
        var sku = new SKU("PROD-123");
        var name = "Sample Product";
        var description = "Sample Description";
        var price = new Price(99.99m);
        var initialStock = 50;
        var category = "Sample Category";

        // Act
        var item = new Item(sku, name, description, price, initialStock, category);

        // Assert
        item.Id.Should().NotBeEmpty();
        item.SKU.Should().Be(sku);
        item.Name.Should().Be(name);
        item.Description.Should().Be(description);
        item.Price.Should().Be(price);
        item.StockLevel.Available.Should().Be(initialStock);
        item.StockLevel.Reserved.Should().Be(0);
        item.StockLevel.Total.Should().Be(initialStock);
        item.Category.Should().Be(category);
        item.IsActive.Should().BeTrue();
        item.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        item.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        item.Version.Should().NotBeNull();
        item.Version.Should().HaveCount(16); // Guid.ToByteArray() produces 16 bytes
    }

    [Fact]
    public void Item_Stock_Operations_Should_Update_Version_And_Time()
    {
        // Arrange
        var item = CreateTestItem();
        var originalUpdatedAt = item.UpdatedAt;
        var originalVersion = item.Version;

        // Act
        Thread.Sleep(1); // Ensure time passes
        item.ReserveStock(10);

        // Assert
        item.UpdatedAt.Should().BeAfter(originalUpdatedAt);
        item.Version.Should().NotEqual(originalVersion);
    }

    [Fact]
    public void Item_Domain_Events_Should_Accumulate_Correctly()
    {
        // Arrange
        var item = CreateTestItem();

        // Act
        item.Update("New Name", "New Description", new Price(30.99m), "New Category");
        item.AdjustStock(200);
        item.ReserveStock(50);
        item.Deactivate();

        // Assert
        item.DomainEvents.Should().HaveCount(5); // Created, Updated, StockAdjusted, StockReserved, Deactivated
        item.DomainEvents.Should().ContainSingle(e => e is ItemCreatedEvent);
        item.DomainEvents.Should().ContainSingle(e => e is ItemUpdatedEvent);
        item.DomainEvents.Should().ContainSingle(e => e is StockAdjustedEvent);
        item.DomainEvents.Should().ContainSingle(e => e is StockReservedEvent);
        item.DomainEvents.Should().ContainSingle(e => e is ItemDeactivatedEvent);
    }

    private static Item CreateTestItem()
    {
        return new Item(
            new SKU("TEST-001"),
            "Test Product",
            "Test Description",
            new Price(25.99m),
            100,
            "Electronics"
        );
    }
}