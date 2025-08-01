using FluentAssertions;
using WorkerService.Application.Commands;
using WorkerService.Application.Validators;
using Xunit;

namespace WorkerService.UnitTests.Validators;

public class ShipOrderCommandValidatorTests
{
    private readonly ShipOrderCommandValidator _validator;

    public ShipOrderCommandValidatorTests()
    {
        _validator = new ShipOrderCommandValidator();
    }

    [Fact]
    public void Validate_ValidCommand_ShouldPassValidation()
    {
        // Arrange
        var command = new ShipOrderCommand(Guid.NewGuid(), "TRACK123456");

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
        var command = new ShipOrderCommand(Guid.Empty, "TRACK123456");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => 
            error.PropertyName == nameof(ShipOrderCommand.OrderId) && 
            error.ErrorMessage == "Order ID is required");
    }

    [Fact]
    public void Validate_EmptyTrackingNumber_ShouldFailValidation()
    {
        // Arrange
        var command = new ShipOrderCommand(Guid.NewGuid(), "");

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => 
            error.PropertyName == nameof(ShipOrderCommand.TrackingNumber) && 
            error.ErrorMessage == "Tracking number is required");
    }

    [Fact]
    public void Validate_TrackingNumberTooLong_ShouldFailValidation()
    {
        // Arrange
        var longTrackingNumber = new string('A', 101); // 101 characters, exceeds max of 100
        var command = new ShipOrderCommand(Guid.NewGuid(), longTrackingNumber);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => 
            error.PropertyName == nameof(ShipOrderCommand.TrackingNumber) && 
            error.ErrorMessage == "Tracking number cannot exceed 100 characters");
    }

    [Fact]
    public void Validate_TrackingNumberAtMaxLength_ShouldPassValidation()
    {
        // Arrange
        var maxLengthTrackingNumber = new string('A', 100); // Exactly 100 characters
        var command = new ShipOrderCommand(Guid.NewGuid(), maxLengthTrackingNumber);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }
}