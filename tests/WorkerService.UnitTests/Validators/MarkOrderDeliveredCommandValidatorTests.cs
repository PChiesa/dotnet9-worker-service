using FluentAssertions;
using WorkerService.Application.Commands;
using WorkerService.Application.Validators;
using Xunit;

namespace WorkerService.UnitTests.Validators;

public class MarkOrderDeliveredCommandValidatorTests
{
    private readonly MarkOrderDeliveredCommandValidator _validator;

    public MarkOrderDeliveredCommandValidatorTests()
    {
        _validator = new MarkOrderDeliveredCommandValidator();
    }

    [Fact]
    public void Validate_ValidCommand_ShouldPassValidation()
    {
        // Arrange
        var command = new MarkOrderDeliveredCommand(Guid.NewGuid());

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
        var command = new MarkOrderDeliveredCommand(Guid.Empty);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => 
            error.PropertyName == nameof(MarkOrderDeliveredCommand.OrderId) && 
            error.ErrorMessage == "Order ID is required");
    }

    [Fact]
    public void Validate_ValidOrderId_ShouldPassValidation()
    {
        // Arrange
        var validOrderId = Guid.NewGuid();
        var command = new MarkOrderDeliveredCommand(validOrderId);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("12345678-1234-1234-1234-123456789abc")]
    [InlineData("87654321-4321-4321-4321-cba987654321")]
    [InlineData("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee")]
    public void Validate_ValidGuidFormats_ShouldPassValidation(string guidString)
    {
        // Arrange
        var validOrderId = Guid.Parse(guidString);
        var command = new MarkOrderDeliveredCommand(validOrderId);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_NewGuid_ShouldPassValidation()
    {
        // Arrange
        var command = new MarkOrderDeliveredCommand(Guid.NewGuid());

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}