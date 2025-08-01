using FluentAssertions;
using WorkerService.Application.Commands;
using WorkerService.Application.Validators;
using Xunit;

namespace WorkerService.UnitTests.Validators;

public class ProcessPaymentCommandValidatorTests
{
    private readonly ProcessPaymentCommandValidator _validator;

    public ProcessPaymentCommandValidatorTests()
    {
        _validator = new ProcessPaymentCommandValidator();
    }

    [Fact]
    public void Validate_ValidCommand_ShouldPassValidation()
    {
        // Arrange
        var command = new ProcessPaymentCommand(Guid.NewGuid());

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
        var command = new ProcessPaymentCommand(Guid.Empty);

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => 
            error.PropertyName == nameof(ProcessPaymentCommand.OrderId) && 
            error.ErrorMessage == "Order ID is required");
    }
}