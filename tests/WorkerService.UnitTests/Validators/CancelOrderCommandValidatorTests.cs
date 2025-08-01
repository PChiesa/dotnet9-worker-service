using FluentAssertions;
using WorkerService.Application.Commands;
using WorkerService.Application.Validators;
using Xunit;

namespace WorkerService.UnitTests.Validators;

public class CancelOrderCommandValidatorTests
{
    private readonly CancelOrderCommandValidator _validator;

    public CancelOrderCommandValidatorTests()
    {
        _validator = new CancelOrderCommandValidator();
    }

    [Fact]
    public void Validate_ValidCommandWithReason_ShouldPassValidation()
    {
        // Arrange
        var command = new CancelOrderCommand(Guid.NewGuid(), "Customer requested cancellation");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ValidCommandWithoutReason_ShouldPassValidation()
    {
        // Arrange
        var command = new CancelOrderCommand(Guid.NewGuid());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ValidCommandWithNullReason_ShouldPassValidation()
    {
        // Arrange
        var command = new CancelOrderCommand(Guid.NewGuid(), null);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ValidCommandWithEmptyReason_ShouldPassValidation()
    {
        // Arrange
        var command = new CancelOrderCommand(Guid.NewGuid(), "");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_EmptyOrderId_ShouldFailValidation()
    {
        // Arrange
        var command = new CancelOrderCommand(Guid.Empty, "Test reason");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => 
            error.PropertyName == nameof(CancelOrderCommand.OrderId) && 
            error.ErrorMessage == "Order ID is required");
    }

    [Fact]
    public void Validate_ReasonTooLong_ShouldFailValidation()
    {
        // Arrange
        var longReason = new string('A', 501); // 501 characters, exceeds max of 500
        var command = new CancelOrderCommand(Guid.NewGuid(), longReason);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => 
            error.PropertyName == nameof(CancelOrderCommand.Reason) && 
            error.ErrorMessage == "Cancel reason cannot exceed 500 characters");
    }

    [Fact]
    public void Validate_ReasonAtMaxLength_ShouldPassValidation()
    {
        // Arrange
        var maxLengthReason = new string('A', 500); // Exactly 500 characters
        var command = new CancelOrderCommand(Guid.NewGuid(), maxLengthReason);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Customer changed mind")]
    [InlineData("Found better deal elsewhere")]
    [InlineData("Product no longer needed")]
    [InlineData("Ordered by mistake")]
    [InlineData("Financial constraints")]
    public void Validate_ValidReasons_ShouldPassValidation(string reason)
    {
        // Arrange
        var command = new CancelOrderCommand(Guid.NewGuid(), reason);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ReasonWithSpecialCharacters_ShouldPassValidation()
    {
        // Arrange
        var reasonWithSpecialChars = "Customer said: \"I don't need this anymore!\" (Ref: #12345)";
        var command = new CancelOrderCommand(Guid.NewGuid(), reasonWithSpecialChars);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_MultipleValidationErrors_ShouldReturnAllErrors()
    {
        // Arrange
        var longReason = new string('A', 501); // Too long
        var command = new CancelOrderCommand(Guid.Empty, longReason); // Empty OrderId and long reason

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
        result.Errors.Should().Contain(error => 
            error.PropertyName == nameof(CancelOrderCommand.OrderId) && 
            error.ErrorMessage == "Order ID is required");
        result.Errors.Should().Contain(error => 
            error.PropertyName == nameof(CancelOrderCommand.Reason) && 
            error.ErrorMessage == "Cancel reason cannot exceed 500 characters");
    }

    [Theory]
    [InlineData("12345678-1234-1234-1234-123456789abc")]
    [InlineData("87654321-4321-4321-4321-cba987654321")]
    [InlineData("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")]
    public void Validate_ValidGuidFormatsWithReason_ShouldPassValidation(string guidString)
    {
        // Arrange
        var validOrderId = Guid.Parse(guidString);
        var command = new CancelOrderCommand(validOrderId, "Valid cancellation reason");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ReasonWithWhitespaceOnly_ShouldPassValidation()
    {
        // Arrange
        var whitespaceReason = "   ";
        var command = new CancelOrderCommand(Guid.NewGuid(), whitespaceReason);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}