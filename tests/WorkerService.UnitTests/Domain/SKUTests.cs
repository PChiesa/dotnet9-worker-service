using FluentAssertions;
using WorkerService.Domain.ValueObjects;
using Xunit;

namespace WorkerService.UnitTests.Domain;

public class SKUTests
{
    [Fact]
    public void SKU_Creation_With_Valid_Value_Should_Set_Value()
    {
        // Arrange
        var validValue = "PROD-123";

        // Act
        var sku = new SKU(validValue);

        // Assert
        sku.Value.Should().Be(validValue);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]

    public void SKU_Creation_With_Empty_Value_Should_Throw_Exception(string invalidValue)
    {
        // Act & Assert
        var action = () => new SKU(invalidValue);
        action.Should().Throw<ArgumentException>()
            .WithMessage("SKU cannot be empty*")
            .WithParameterName("value");
    }

    [Fact]
    public void SKU_Creation_With_Long_Value_Should_Throw_Exception()
    {
        // Arrange
        var longValue = new string('A', 51); // Exceeds 50 character limit

        // Act & Assert
        var action = () => new SKU(longValue);
        action.Should().Throw<ArgumentException>()
            .WithMessage("SKU cannot exceed 50 characters*")
            .WithParameterName("value");
    }

    [Theory]
    [InlineData("prod-123")] // lowercase
    [InlineData("PROD_123")] // underscore
    [InlineData("PROD 123")] // space
    [InlineData("PROD@123")] // special character
    [InlineData("PROD.123")] // dot
    public void SKU_Creation_With_Invalid_Characters_Should_Throw_Exception(string invalidValue)
    {
        // Act & Assert
        var action = () => new SKU(invalidValue);
        action.Should().Throw<ArgumentException>()
            .WithMessage("SKU must contain only uppercase letters, numbers, and hyphens*")
            .WithParameterName("value");
    }

    [Theory]
    [InlineData("ABC123")]
    [InlineData("PROD-001")]
    [InlineData("A")]
    [InlineData("123")]
    [InlineData("A-B-C-1-2-3")]
    [InlineData("ELECTRONICS-PHONE-001")]
    public void SKU_Creation_With_Valid_Characters_Should_Succeed(string validValue)
    {
        // Act
        var sku = new SKU(validValue);

        // Assert
        sku.Value.Should().Be(validValue);
    }

    [Fact]
    public void SKU_Equality_With_Same_Value_Should_Return_True()
    {
        // Arrange
        var value = "PROD-123";
        var sku1 = new SKU(value);
        var sku2 = new SKU(value);

        // Act & Assert
        sku1.Equals(sku2).Should().BeTrue();
        (sku1 == sku2).Should().BeFalse(); // No == operator overload
        sku1.GetHashCode().Should().Be(sku2.GetHashCode());
    }

    [Fact]
    public void SKU_Equality_With_Different_Value_Should_Return_False()
    {
        // Arrange
        var sku1 = new SKU("PROD-123");
        var sku2 = new SKU("PROD-456");

        // Act & Assert
        sku1.Equals(sku2).Should().BeFalse();
        sku1.GetHashCode().Should().NotBe(sku2.GetHashCode());
    }

    [Fact]
    public void SKU_Equals_With_Null_Should_Return_False()
    {
        // Arrange
        var sku = new SKU("PROD-123");

        // Act & Assert
        sku.Equals(null).Should().BeFalse();        
    }

    [Fact]
    public void SKU_Equals_With_Different_Type_Should_Return_False()
    {
        // Arrange
        var sku = new SKU("PROD-123");
        var stringValue = "PROD-123";

        // Act & Assert
        sku.Equals(stringValue).Should().BeFalse();
    }

    [Fact]
    public void SKU_ToString_Should_Return_Value()
    {
        // Arrange
        var value = "PROD-123";
        var sku = new SKU(value);

        // Act
        var result = sku.ToString();

        // Assert
        result.Should().Be(value);
    }

    [Fact]
    public void SKU_Implicit_Conversion_To_String_Should_Return_Value()
    {
        // Arrange
        var value = "PROD-123";
        var sku = new SKU(value);

        // Act
        string result = sku;

        // Assert
        result.Should().Be(value);
    }

    [Fact]
    public void SKU_Explicit_Conversion_From_String_Should_Create_SKU()
    {
        // Arrange
        var value = "PROD-123";

        // Act
        var sku = (SKU)value;

        // Assert
        sku.Value.Should().Be(value);
    }

    [Fact]
    public void SKU_GetHashCode_Should_Be_Consistent()
    {
        // Arrange
        var value = "PROD-123";
        var sku = new SKU(value);

        // Act
        var hashCode1 = sku.GetHashCode();
        var hashCode2 = sku.GetHashCode();

        // Assert
        hashCode1.Should().Be(hashCode2);
    }

    [Fact]
    public void SKU_Equality_Operator_With_Same_Reference_Should_Return_True()
    {
        // Arrange
        var sku = new SKU("PROD-123");

        // Act & Assert
        (sku.Equals(sku)).Should().BeTrue();
    }

    [Theory]
    [InlineData("A")]
    [InlineData("AB")]
    [InlineData("ABC")]
    [InlineData("A1")]
    [InlineData("1A")]
    [InlineData("123")]
    public void SKU_Creation_With_Various_Valid_Lengths_Should_Succeed(string value)
    {
        // Act
        var sku = new SKU(value);

        // Assert
        sku.Value.Should().Be(value);
    }

    [Fact]
    public void SKU_Creation_With_Maximum_Length_Should_Succeed()
    {
        // Arrange
        var maxLengthValue = new string('A', 50); // Exactly 50 characters

        // Act
        var sku = new SKU(maxLengthValue);

        // Assert
        sku.Value.Should().Be(maxLengthValue);
        sku.Value.Length.Should().Be(50);
    }

    [Fact]
    public void SKU_With_Hyphens_Should_Be_Valid()
    {
        // Arrange
        var valueWithHyphens = "CATEGORY-SUBCATEGORY-PRODUCT-001";

        // Act
        var sku = new SKU(valueWithHyphens);

        // Assert
        sku.Value.Should().Be(valueWithHyphens);
    }

    [Fact]
    public void SKU_With_Only_Numbers_Should_Be_Valid()
    {
        // Arrange
        var numericValue = "123456789";

        // Act
        var sku = new SKU(numericValue);

        // Assert
        sku.Value.Should().Be(numericValue);
    }

    [Fact]
    public void SKU_With_Only_Letters_Should_Be_Valid()
    {
        // Arrange
        var letterValue = "ABCDEFGHIJK";

        // Act
        var sku = new SKU(letterValue);

        // Assert
        sku.Value.Should().Be(letterValue);
    }
}