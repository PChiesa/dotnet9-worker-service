using FluentAssertions;
using WorkerService.Domain.ValueObjects;
using Xunit;

namespace WorkerService.UnitTests.Domain;

public class StockLevelTests
{
    [Fact]
    public void StockLevel_Creation_With_Valid_Values_Should_Set_Properties()
    {
        // Arrange
        var available = 100;
        var reserved = 25;

        // Act
        var stockLevel = new StockLevel(available, reserved);

        // Assert
        stockLevel.Available.Should().Be(available);
        stockLevel.Reserved.Should().Be(reserved);
        stockLevel.Total.Should().Be(available + reserved);
    }

    [Fact]
    public void StockLevel_Creation_With_Default_Reserved_Should_Use_Zero()
    {
        // Arrange
        var available = 100;

        // Act
        var stockLevel = new StockLevel(available);

        // Assert
        stockLevel.Available.Should().Be(available);
        stockLevel.Reserved.Should().Be(0);
        stockLevel.Total.Should().Be(available);
    }

    [Fact]
    public void StockLevel_Creation_With_Negative_Available_Should_Throw_Exception()
    {
        // Arrange
        var negativeAvailable = -10;

        // Act & Assert
        var action = () => new StockLevel(negativeAvailable);
        action.Should().Throw<ArgumentException>()
            .WithMessage("Available stock cannot be negative*")
            .WithParameterName("available");
    }

    [Fact]
    public void StockLevel_Creation_With_Negative_Reserved_Should_Throw_Exception()
    {
        // Arrange
        var available = 100;
        var negativeReserved = -5;

        // Act & Assert
        var action = () => new StockLevel(available, negativeReserved);
        action.Should().Throw<ArgumentException>()
            .WithMessage("Reserved stock cannot be negative*")
            .WithParameterName("reserved");
    }

    [Fact]
    public void Reserve_With_Valid_Quantity_Should_Move_Stock_From_Available_To_Reserved()
    {
        // Arrange
        var stockLevel = new StockLevel(100, 10);
        var reserveQuantity = 25;

        // Act
        var newStockLevel = stockLevel.Reserve(reserveQuantity);

        // Assert
        newStockLevel.Available.Should().Be(75); // 100 - 25
        newStockLevel.Reserved.Should().Be(35); // 10 + 25
        newStockLevel.Total.Should().Be(110); // Same total
    }

    [Fact]
    public void Reserve_With_Zero_Quantity_Should_Throw_Exception()
    {
        // Arrange
        var stockLevel = new StockLevel(100);

        // Act & Assert
        var action = () => stockLevel.Reserve(0);
        action.Should().Throw<ArgumentException>()
            .WithMessage("Reserve quantity must be positive*")
            .WithParameterName("quantity");
    }

    [Fact]
    public void Reserve_With_Negative_Quantity_Should_Throw_Exception()
    {
        // Arrange
        var stockLevel = new StockLevel(100);

        // Act & Assert
        var action = () => stockLevel.Reserve(-5);
        action.Should().Throw<ArgumentException>()
            .WithMessage("Reserve quantity must be positive*")
            .WithParameterName("quantity");
    }

    [Fact]
    public void Reserve_More_Than_Available_Should_Throw_Exception()
    {
        // Arrange
        var stockLevel = new StockLevel(50, 10);
        var reserveQuantity = 60; // More than available

        // Act & Assert
        var action = () => stockLevel.Reserve(reserveQuantity);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot reserve 60 items. Only 50 available.");
    }

    [Fact]
    public void Reserve_Exact_Available_Amount_Should_Succeed()
    {
        // Arrange
        var stockLevel = new StockLevel(50, 10);
        var reserveQuantity = 50; // Exact available amount

        // Act
        var newStockLevel = stockLevel.Reserve(reserveQuantity);

        // Assert
        newStockLevel.Available.Should().Be(0);
        newStockLevel.Reserved.Should().Be(60); // 10 + 50
        newStockLevel.Total.Should().Be(60);
    }

    [Fact]
    public void Release_With_Valid_Quantity_Should_Move_Stock_From_Reserved_To_Available()
    {
        // Arrange
        var stockLevel = new StockLevel(75, 35);
        var releaseQuantity = 15;

        // Act
        var newStockLevel = stockLevel.Release(releaseQuantity);

        // Assert
        newStockLevel.Available.Should().Be(90); // 75 + 15
        newStockLevel.Reserved.Should().Be(20); // 35 - 15
        newStockLevel.Total.Should().Be(110); // Same total
    }

    [Fact]
    public void Release_With_Zero_Quantity_Should_Throw_Exception()
    {
        // Arrange
        var stockLevel = new StockLevel(75, 35);

        // Act & Assert
        var action = () => stockLevel.Release(0);
        action.Should().Throw<ArgumentException>()
            .WithMessage("Release quantity must be positive*")
            .WithParameterName("quantity");
    }

    [Fact]
    public void Release_With_Negative_Quantity_Should_Throw_Exception()
    {
        // Arrange
        var stockLevel = new StockLevel(75, 35);

        // Act & Assert
        var action = () => stockLevel.Release(-5);
        action.Should().Throw<ArgumentException>()
            .WithMessage("Release quantity must be positive*")
            .WithParameterName("quantity");
    }

    [Fact]
    public void Release_More_Than_Reserved_Should_Throw_Exception()
    {
        // Arrange
        var stockLevel = new StockLevel(75, 35);
        var releaseQuantity = 40; // More than reserved

        // Act & Assert
        var action = () => stockLevel.Release(releaseQuantity);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot release 40 items. Only 35 reserved.");
    }

    [Fact]
    public void Release_Exact_Reserved_Amount_Should_Succeed()
    {
        // Arrange
        var stockLevel = new StockLevel(75, 35);
        var releaseQuantity = 35; // Exact reserved amount

        // Act
        var newStockLevel = stockLevel.Release(releaseQuantity);

        // Assert
        newStockLevel.Available.Should().Be(110); // 75 + 35
        newStockLevel.Reserved.Should().Be(0);
        newStockLevel.Total.Should().Be(110);
    }

    [Fact]
    public void Adjust_With_Valid_Amount_Should_Set_Available_Stock()
    {
        // Arrange
        var stockLevel = new StockLevel(100, 25);
        var newAvailable = 150;

        // Act
        var newStockLevel = stockLevel.Adjust(newAvailable);

        // Assert
        newStockLevel.Available.Should().Be(newAvailable);
        newStockLevel.Reserved.Should().Be(25); // Reserved unchanged
        newStockLevel.Total.Should().Be(175);
    }

    [Fact]
    public void Adjust_With_Negative_Amount_Should_Throw_Exception()
    {
        // Arrange
        var stockLevel = new StockLevel(100, 25);

        // Act & Assert
        var action = () => stockLevel.Adjust(-10);
        action.Should().Throw<ArgumentException>()
            .WithMessage("Stock level cannot be negative*")
            .WithParameterName("newAvailable");
    }

    [Fact]
    public void Adjust_To_Zero_Should_Succeed()
    {
        // Arrange
        var stockLevel = new StockLevel(100, 25);

        // Act
        var newStockLevel = stockLevel.Adjust(0);

        // Assert
        newStockLevel.Available.Should().Be(0);
        newStockLevel.Reserved.Should().Be(25);
        newStockLevel.Total.Should().Be(25);
    }

    [Fact]
    public void Commit_With_Valid_Quantity_Should_Remove_From_Reserved()
    {
        // Arrange
        var stockLevel = new StockLevel(75, 35);
        var commitQuantity = 20;

        // Act
        var newStockLevel = stockLevel.Commit(commitQuantity);

        // Assert
        newStockLevel.Available.Should().Be(75); // Available unchanged
        newStockLevel.Reserved.Should().Be(15); // 35 - 20
        newStockLevel.Total.Should().Be(90); // Total reduced by committed amount
    }

    [Fact]
    public void Commit_With_Zero_Quantity_Should_Throw_Exception()
    {
        // Arrange
        var stockLevel = new StockLevel(75, 35);

        // Act & Assert
        var action = () => stockLevel.Commit(0);
        action.Should().Throw<ArgumentException>()
            .WithMessage("Commit quantity must be positive*")
            .WithParameterName("quantity");
    }

    [Fact]
    public void Commit_With_Negative_Quantity_Should_Throw_Exception()
    {
        // Arrange
        var stockLevel = new StockLevel(75, 35);

        // Act & Assert
        var action = () => stockLevel.Commit(-5);
        action.Should().Throw<ArgumentException>()
            .WithMessage("Commit quantity must be positive*")
            .WithParameterName("quantity");
    }

    [Fact]
    public void Commit_More_Than_Reserved_Should_Throw_Exception()
    {
        // Arrange
        var stockLevel = new StockLevel(75, 35);
        var commitQuantity = 40; // More than reserved

        // Act & Assert
        var action = () => stockLevel.Commit(commitQuantity);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("Cannot commit 40 items. Only 35 reserved.");
    }

    [Fact]
    public void Commit_Exact_Reserved_Amount_Should_Succeed()
    {
        // Arrange
        var stockLevel = new StockLevel(75, 35);
        var commitQuantity = 35; // Exact reserved amount

        // Act
        var newStockLevel = stockLevel.Commit(commitQuantity);

        // Assert
        newStockLevel.Available.Should().Be(75);
        newStockLevel.Reserved.Should().Be(0);
        newStockLevel.Total.Should().Be(75);
    }

    [Fact]
    public void StockLevel_Equality_With_Same_Values_Should_Return_True()
    {
        // Arrange
        var stockLevel1 = new StockLevel(100, 25);
        var stockLevel2 = new StockLevel(100, 25);

        // Act & Assert
        stockLevel1.Equals(stockLevel2).Should().BeTrue();
        stockLevel1.GetHashCode().Should().Be(stockLevel2.GetHashCode());
    }

    [Fact]
    public void StockLevel_Equality_With_Different_Available_Should_Return_False()
    {
        // Arrange
        var stockLevel1 = new StockLevel(100, 25);
        var stockLevel2 = new StockLevel(90, 25);

        // Act & Assert
        stockLevel1.Equals(stockLevel2).Should().BeFalse();
        stockLevel1.GetHashCode().Should().NotBe(stockLevel2.GetHashCode());
    }

    [Fact]
    public void StockLevel_Equality_With_Different_Reserved_Should_Return_False()
    {
        // Arrange
        var stockLevel1 = new StockLevel(100, 25);
        var stockLevel2 = new StockLevel(100, 30);

        // Act & Assert
        stockLevel1.Equals(stockLevel2).Should().BeFalse();
        stockLevel1.GetHashCode().Should().NotBe(stockLevel2.GetHashCode());
    }

    [Fact]
    public void StockLevel_Equals_With_Null_Should_Return_False()
    {
        // Arrange
        var stockLevel = new StockLevel(100, 25);

        // Act & Assert
        stockLevel.Equals(null).Should().BeFalse();
        stockLevel.Equals((object?)null).Should().BeFalse();
    }

    [Fact]
    public void StockLevel_Equals_With_Different_Type_Should_Return_False()
    {
        // Arrange
        var stockLevel = new StockLevel(100, 25);
        var stringValue = "100,25";

        // Act & Assert
        stockLevel.Equals(stringValue).Should().BeFalse();
    }

    [Fact]
    public void StockLevel_ToString_Should_Return_Formatted_String()
    {
        // Arrange
        var stockLevel = new StockLevel(100, 25);

        // Act
        var result = stockLevel.ToString();

        // Assert
        result.Should().Be("Available: 100, Reserved: 25");
    }

    [Theory]
    [InlineData(0, 0, "Available: 0, Reserved: 0")]
    [InlineData(1, 0, "Available: 1, Reserved: 0")]
    [InlineData(0, 1, "Available: 0, Reserved: 1")]
    [InlineData(999, 111, "Available: 999, Reserved: 111")]
    public void StockLevel_ToString_Should_Format_Various_Values_Correctly(int available, int reserved, string expected)
    {
        // Arrange
        var stockLevel = new StockLevel(available, reserved);

        // Act
        var result = stockLevel.ToString();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void StockLevel_GetHashCode_Should_Be_Consistent()
    {
        // Arrange
        var stockLevel = new StockLevel(100, 25);

        // Act
        var hashCode1 = stockLevel.GetHashCode();
        var hashCode2 = stockLevel.GetHashCode();

        // Assert
        hashCode1.Should().Be(hashCode2);
    }

    [Fact]
    public void StockLevel_Total_Property_Should_Always_Return_Sum()
    {
        // Arrange & Act & Assert
        var stockLevel1 = new StockLevel(100, 25);
        stockLevel1.Total.Should().Be(125);

        var stockLevel2 = new StockLevel(0, 50);
        stockLevel2.Total.Should().Be(50);

        var stockLevel3 = new StockLevel(75, 0);
        stockLevel3.Total.Should().Be(75);

        var stockLevel4 = new StockLevel(0, 0);
        stockLevel4.Total.Should().Be(0);
    }

    [Fact]
    public void StockLevel_Complete_Workflow_Should_Work_Correctly()
    {
        // Arrange
        var stockLevel = new StockLevel(100); // Start with 100 available

        // Act & Assert - Reserve some stock
        var afterReserve = stockLevel.Reserve(30);
        afterReserve.Available.Should().Be(70);
        afterReserve.Reserved.Should().Be(30);
        afterReserve.Total.Should().Be(100);

        // Act & Assert - Release some reserved stock
        var afterRelease = afterReserve.Release(10);
        afterRelease.Available.Should().Be(80);
        afterRelease.Reserved.Should().Be(20);
        afterRelease.Total.Should().Be(100);

        // Act & Assert - Commit some reserved stock
        var afterCommit = afterRelease.Commit(15);
        afterCommit.Available.Should().Be(80);
        afterCommit.Reserved.Should().Be(5);
        afterCommit.Total.Should().Be(85); // Total reduced by committed amount

        // Act & Assert - Adjust available stock
        var afterAdjust = afterCommit.Adjust(120);
        afterAdjust.Available.Should().Be(120);
        afterAdjust.Reserved.Should().Be(5);
        afterAdjust.Total.Should().Be(125);
    }

    [Fact]
    public void StockLevel_Edge_Cases_Should_Work_Correctly()
    {
        // Test with zero values
        var zeroStock = new StockLevel(0, 0);
        zeroStock.Available.Should().Be(0);
        zeroStock.Reserved.Should().Be(0);
        zeroStock.Total.Should().Be(0);

        // Test with large values
        var largeStock = new StockLevel(int.MaxValue - 1000, 1000);
        largeStock.Available.Should().Be(int.MaxValue - 1000);
        largeStock.Reserved.Should().Be(1000);
        largeStock.Total.Should().Be(int.MaxValue);
    }
}