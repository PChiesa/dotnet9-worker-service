using FluentAssertions;
using FluentValidation.TestHelper;
using WorkerService.Application.Commands;
using WorkerService.Application.Validators;
using Xunit;

namespace WorkerService.UnitTests.Validators;

public class UpdateItemCommandValidatorTests
{
    private readonly UpdateItemCommandValidator _validator;

    public UpdateItemCommandValidatorTests()
    {
        _validator = new UpdateItemCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_ShouldPassValidation()
    {
        // Arrange
        var command = new UpdateItemCommand(
            Guid.NewGuid(),
            "Updated Product",
            "Updated Description",
            35.99m,
            "Updated Category");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyItemId_ShouldHaveValidationError()
    {
        // Arrange
        var command = new UpdateItemCommand(
            Guid.Empty,
            "Updated Product",
            "Updated Description",
            35.99m,
            "Updated Category");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ItemId)
            .WithErrorMessage("Item ID is required");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_WithEmptyName_ShouldHaveValidationError(string emptyName)
    {
        // Arrange
        var command = new UpdateItemCommand(
            Guid.NewGuid(),
            emptyName,
            "Updated Description",
            35.99m,
            "Updated Category");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Name is required");
    }

    [Fact]
    public void Validate_WithLongName_ShouldHaveValidationError()
    {
        // Arrange
        var longName = new string('A', 201); // 201 characters, exceeds limit
        var command = new UpdateItemCommand(
            Guid.NewGuid(),
            longName,
            "Updated Description",
            35.99m,
            "Updated Category");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage("Name cannot exceed 200 characters");
    }

    [Fact]
    public void Validate_WithMaxLengthName_ShouldPassValidation()
    {
        // Arrange
        var maxLengthName = new string('A', 200); // Exactly 200 characters
        var command = new UpdateItemCommand(
            Guid.NewGuid(),
            maxLengthName,
            "Updated Description",
            35.99m,
            "Updated Category");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_WithLongDescription_ShouldHaveValidationError()
    {
        // Arrange
        var longDescription = new string('A', 1001); // 1001 characters, exceeds limit
        var command = new UpdateItemCommand(
            Guid.NewGuid(),
            "Updated Product",
            longDescription,
            35.99m,
            "Updated Category");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Description)
            .WithErrorMessage("Description cannot exceed 1000 characters");
    }

    [Fact]
    public void Validate_WithEmptyDescription_ShouldPassValidation()
    {
        // Arrange
        var command = new UpdateItemCommand(
            Guid.NewGuid(),
            "Updated Product",
            "", // Empty description should be allowed
            35.99m,
            "Updated Category");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_WithNullDescription_ShouldPassValidation()
    {
        // Arrange
        var command = new UpdateItemCommand(
            Guid.NewGuid(),
            "Updated Product",
            null, // Null description should be allowed
            35.99m,
            "Updated Category");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10.99)]
    public void Validate_WithZeroOrNegativePrice_ShouldHaveValidationError(decimal invalidPrice)
    {
        // Arrange
        var command = new UpdateItemCommand(
            Guid.NewGuid(),
            "Updated Product",
            "Updated Description",
            invalidPrice,
            "Updated Category");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Price)
            .WithErrorMessage("Price must be greater than zero");
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(1.00)]
    [InlineData(99.99)]
    [InlineData(999.99)]
    public void Validate_WithValidPrice_ShouldPassValidation(decimal validPrice)
    {
        // Arrange
        var command = new UpdateItemCommand(
            Guid.NewGuid(),
            "Updated Product",
            "Updated Description",
            validPrice,
            "Updated Category");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Price);
    }

    [Theory]
    [InlineData(25.999)] // 3 decimal places
    [InlineData(25.9999)] // 4 decimal places
    public void Validate_WithTooManyDecimalPlaces_ShouldHaveValidationError(decimal invalidPrice)
    {
        // Arrange
        var command = new UpdateItemCommand(
            Guid.NewGuid(),
            "Updated Product",
            "Updated Description",
            invalidPrice,
            "Updated Category");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Price)
            .WithErrorMessage("Price cannot have more than 2 decimal places");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_WithEmptyCategory_ShouldHaveValidationError(string emptyCategory)
    {
        // Arrange
        var command = new UpdateItemCommand(
            Guid.NewGuid(),
            "Updated Product",
            "Updated Description",
            35.99m,
            emptyCategory);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Category)
            .WithErrorMessage("Category is required");
    }

    [Fact]
    public void Validate_WithLongCategory_ShouldHaveValidationError()
    {
        // Arrange
        var longCategory = new string('A', 101); // 101 characters, exceeds limit
        var command = new UpdateItemCommand(
            Guid.NewGuid(),
            "Updated Product",
            "Updated Description",
            35.99m,
            longCategory);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Category)
            .WithErrorMessage("Category cannot exceed 100 characters");
    }

    [Fact]
    public void Validate_WithMaxLengthCategory_ShouldPassValidation()
    {
        // Arrange
        var maxLengthCategory = new string('A', 100); // Exactly 100 characters
        var command = new UpdateItemCommand(
            Guid.NewGuid(),
            "Updated Product",
            "Updated Description",
            35.99m,
            maxLengthCategory);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Category);
    }

    [Fact]
    public void Validate_WithMultipleErrors_ShouldHaveAllValidationErrors()
    {
        // Arrange
        var command = new UpdateItemCommand(
            Guid.Empty, // Empty GUID
            "", // Empty name
            new string('A', 1001), // Long description
            -10.99m, // Negative price
            ""); // Empty category

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ItemId);
        result.ShouldHaveValidationErrorFor(x => x.Name);
        result.ShouldHaveValidationErrorFor(x => x.Description);
        result.ShouldHaveValidationErrorFor(x => x.Price);
        result.ShouldHaveValidationErrorFor(x => x.Category);
    }

    [Theory]
    [InlineData("Electronics")]
    [InlineData("Books")]
    [InlineData("Clothing")]
    [InlineData("Home & Garden")]
    [InlineData("Sports & Outdoors")]
    public void Validate_WithValidCategory_ShouldPassValidation(string validCategory)
    {
        // Arrange
        var command = new UpdateItemCommand(
            Guid.NewGuid(),
            "Updated Product",
            "Updated Description",
            35.99m,
            validCategory);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Category);
    }

    [Fact]
    public void Validate_WithComplexValidCommand_ShouldPassAllValidations()
    {
        // Arrange
        var command = new UpdateItemCommand(
            Guid.NewGuid(),
            "Updated iPhone 15 Pro Max 512GB Space Black",
            "Updated description: The iPhone 15 Pro Max now features enhanced camera capabilities, improved battery life, and the latest iOS with advanced AI features for productivity and creativity.",
            1399.99m,
            "Premium Electronics");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithMaxLengthDescription_ShouldPassValidation()
    {
        // Arrange
        var maxLengthDescription = new string('A', 1000); // Exactly 1000 characters
        var command = new UpdateItemCommand(
            Guid.NewGuid(),
            "Updated Product",
            maxLengthDescription,
            35.99m,
            "Updated Category");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_WithValidGuid_ShouldPassValidation()
    {
        // Arrange
        var validGuid = Guid.NewGuid();
        var command = new UpdateItemCommand(
            validGuid,
            "Updated Product",
            "Updated Description",
            35.99m,
            "Updated Category");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.ItemId);
    }

    [Theory]
    [InlineData(10.00)]
    [InlineData(10.50)]
    [InlineData(10.99)]
    public void Validate_WithTwoDecimalPlaces_ShouldPassValidation(decimal validPrice)
    {
        // Arrange
        var command = new UpdateItemCommand(
            Guid.NewGuid(),
            "Updated Product",
            "Updated Description",
            validPrice,
            "Updated Category");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Price);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(100)]
    [InlineData(1000)]
    public void Validate_WithWholeNumberPrice_ShouldPassValidation(decimal wholePrice)
    {
        // Arrange
        var command = new UpdateItemCommand(
            Guid.NewGuid(),
            "Updated Product",
            "Updated Description",
            wholePrice,
            "Updated Category");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Price);
    }
}