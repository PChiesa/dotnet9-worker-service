using FluentAssertions;
using FluentValidation.TestHelper;
using WorkerService.Application.Commands;
using WorkerService.Application.Validators;
using Xunit;

namespace WorkerService.UnitTests.Validators;

public class CreateItemCommandValidatorTests
{
    private readonly CreateItemCommandValidator _validator;

    public CreateItemCommandValidatorTests()
    {
        _validator = new CreateItemCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_ShouldPassValidation()
    {
        // Arrange
        var command = new CreateItemCommand(
            "PROD-001",
            "Test Product",
            "Test Description",
            25.99m,
            100,
            "Electronics");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_WithEmptySKU_ShouldHaveValidationError(string emptySku)
    {
        // Arrange
        var command = new CreateItemCommand(
            emptySku,
            "Test Product",
            "Test Description",
            25.99m,
            100,
            "Electronics");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SKU)
            .WithErrorMessage("SKU is required");
    }

    [Fact]
    public void Validate_WithLongSKU_ShouldHaveValidationError()
    {
        // Arrange
        var longSku = new string('A', 51); // 51 characters, exceeds limit
        var command = new CreateItemCommand(
            longSku,
            "Test Product",
            "Test Description",
            25.99m,
            100,
            "Electronics");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SKU)
            .WithErrorMessage("SKU cannot exceed 50 characters");
    }

    [Theory]
    [InlineData("prod-001")] // lowercase
    [InlineData("PROD_001")] // underscore
    [InlineData("PROD 001")] // space
    [InlineData("PROD@001")] // special character
    [InlineData("PROD.001")] // dot
    public void Validate_WithInvalidSKUFormat_ShouldHaveValidationError(string invalidSku)
    {
        // Arrange
        var command = new CreateItemCommand(
            invalidSku,
            "Test Product",
            "Test Description",
            25.99m,
            100,
            "Electronics");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SKU)
            .WithErrorMessage("SKU must contain only uppercase letters, numbers, and hyphens");
    }

    [Theory]
    [InlineData("PROD001")]
    [InlineData("PROD-001")]
    [InlineData("ABC123")]
    [InlineData("A")]
    [InlineData("123")]
    [InlineData("A-B-C-1-2-3")]
    public void Validate_WithValidSKUFormat_ShouldPassValidation(string validSku)
    {
        // Arrange
        var command = new CreateItemCommand(
            validSku,
            "Test Product",
            "Test Description",
            25.99m,
            100,
            "Electronics");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.SKU);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_WithEmptyName_ShouldHaveValidationError(string emptyName)
    {
        // Arrange
        var command = new CreateItemCommand(
            "PROD-001",
            emptyName,
            "Test Description",
            25.99m,
            100,
            "Electronics");

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
        var command = new CreateItemCommand(
            "PROD-001",
            longName,
            "Test Description",
            25.99m,
            100,
            "Electronics");

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
        var command = new CreateItemCommand(
            "PROD-001",
            maxLengthName,
            "Test Description",
            25.99m,
            100,
            "Electronics");

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
        var command = new CreateItemCommand(
            "PROD-001",
            "Test Product",
            longDescription,
            25.99m,
            100,
            "Electronics");

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
        var command = new CreateItemCommand(
            "PROD-001",
            "Test Product",
            "", // Empty description should be allowed
            25.99m,
            100,
            "Electronics");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_WithNullDescription_ShouldPassValidation()
    {
        // Arrange
        var command = new CreateItemCommand(
            "PROD-001",
            "Test Product",
            null, // Null description should be allowed
            25.99m,
            100,
            "Electronics");

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
        var command = new CreateItemCommand(
            "PROD-001",
            "Test Product",
            "Test Description",
            invalidPrice,
            100,
            "Electronics");

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
        var command = new CreateItemCommand(
            "PROD-001",
            "Test Product",
            "Test Description",
            validPrice,
            100,
            "Electronics");

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
        var command = new CreateItemCommand(
            "PROD-001",
            "Test Product",
            "Test Description",
            invalidPrice,
            100,
            "Electronics");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Price)
            .WithErrorMessage("Price cannot have more than 2 decimal places");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-10)]
    [InlineData(-100)]
    public void Validate_WithNegativeInitialStock_ShouldHaveValidationError(int negativeStock)
    {
        // Arrange
        var command = new CreateItemCommand(
            "PROD-001",
            "Test Product",
            "Test Description",
            25.99m,
            negativeStock,
            "Electronics");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.InitialStock)
            .WithErrorMessage("Initial stock cannot be negative");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1000)]
    public void Validate_WithValidInitialStock_ShouldPassValidation(int validStock)
    {
        // Arrange
        var command = new CreateItemCommand(
            "PROD-001",
            "Test Product",
            "Test Description",
            25.99m,
            validStock,
            "Electronics");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.InitialStock);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Validate_WithEmptyCategory_ShouldHaveValidationError(string emptyCategory)
    {
        // Arrange
        var command = new CreateItemCommand(
            "PROD-001",
            "Test Product",
            "Test Description",
            25.99m,
            100,
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
        var command = new CreateItemCommand(
            "PROD-001",
            "Test Product",
            "Test Description",
            25.99m,
            100,
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
        var command = new CreateItemCommand(
            "PROD-001",
            "Test Product",
            "Test Description",
            25.99m,
            100,
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
        var command = new CreateItemCommand(
            "", // Empty SKU
            "", // Empty name
            new string('A', 1001), // Long description
            -10.99m, // Negative price
            -5, // Negative stock
            ""); // Empty category

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SKU);
        result.ShouldHaveValidationErrorFor(x => x.Name);
        result.ShouldHaveValidationErrorFor(x => x.Description);
        result.ShouldHaveValidationErrorFor(x => x.Price);
        result.ShouldHaveValidationErrorFor(x => x.InitialStock);
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
        var command = new CreateItemCommand(
            "PROD-001",
            "Test Product",
            "Test Description",
            25.99m,
            100,
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
        var command = new CreateItemCommand(
            "ELECTRONICS-SMARTPHONE-IPHONE-15-PRO-MAX-256GB",
            "iPhone 15 Pro Max 256GB Natural Titanium",
            "The iPhone 15 Pro Max features a 6.7-inch Super Retina XDR display with ProMotion technology, A17 Pro chip, Pro camera system with 48MP Main camera, and up to 29 hours of video playback.",
            1199.99m,
            50,
            "Electronics");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}