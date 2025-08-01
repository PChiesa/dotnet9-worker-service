using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using WorkerService.Application.Commands;
using WorkerService.Application.Common.Extensions;
using WorkerService.Application.Queries;
using WorkerService.Worker.Controllers;
using Xunit;

namespace WorkerService.UnitTests.Controllers;

public class OrdersControllerTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly Mock<IValidator<CreateOrderCommand>> _mockCreateValidator;
    private readonly Mock<IValidator<UpdateOrderCommand>> _mockUpdateValidator;
    private readonly Mock<IValidator<ProcessPaymentCommand>> _mockProcessPaymentValidator;
    private readonly Mock<IValidator<ShipOrderCommand>> _mockShipOrderValidator;
    private readonly Mock<IValidator<MarkOrderDeliveredCommand>> _mockMarkDeliveredValidator;
    private readonly Mock<IValidator<CancelOrderCommand>> _mockCancelOrderValidator;
    private readonly Mock<ILogger<OrdersController>> _mockLogger;
    private readonly OrdersController _controller;

    public OrdersControllerTests()
    {
        _mockMediator = new Mock<IMediator>();
        _mockCreateValidator = new Mock<IValidator<CreateOrderCommand>>();
        _mockUpdateValidator = new Mock<IValidator<UpdateOrderCommand>>();
        _mockProcessPaymentValidator = new Mock<IValidator<ProcessPaymentCommand>>();
        _mockShipOrderValidator = new Mock<IValidator<ShipOrderCommand>>();
        _mockMarkDeliveredValidator = new Mock<IValidator<MarkOrderDeliveredCommand>>();
        _mockCancelOrderValidator = new Mock<IValidator<CancelOrderCommand>>();
        _mockLogger = new Mock<ILogger<OrdersController>>();
        
        _controller = new OrdersController(
            _mockMediator.Object,
            _mockCreateValidator.Object,
            _mockUpdateValidator.Object,
            _mockProcessPaymentValidator.Object,
            _mockShipOrderValidator.Object,
            _mockMarkDeliveredValidator.Object,
            _mockCancelOrderValidator.Object,
            _mockLogger.Object);
    }

    #region CreateOrder Tests

    [Fact]
    public async Task CreateOrder_WithValidCommand_ShouldReturnCreatedResult()
    {
        // Arrange
        var command = new CreateOrderCommand(
            "CUST001",
            new List<OrderItemDto>
            {
                new("PROD001", 2, 10.00m)
            });

        var expectedResult = new CreateOrderResult(
            Guid.NewGuid(),
            "CUST001",
            20.00m,
            DateTime.UtcNow);

        _mockCreateValidator
            .Setup(v => v.ValidateAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _mockMediator
            .Setup(m => m.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.CreateOrder(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<CreatedAtActionResult>();
        var createdResult = result as CreatedAtActionResult;
        createdResult!.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdResult.Value.Should().Be(expectedResult);
        createdResult.ActionName.Should().Be(nameof(OrdersController.GetOrder));
        createdResult.RouteValues!["id"].Should().Be(expectedResult.OrderId);
    }

    [Fact]
    public async Task CreateOrder_WithInvalidCommand_ShouldReturnBadRequest()
    {
        // Arrange
        var command = new CreateOrderCommand("", new List<OrderItemDto>());
        
        var validationResult = new ValidationResult(new[]
        {
            new ValidationFailure("CustomerId", "Customer ID is required"),
            new ValidationFailure("Items", "At least one item is required")
        });

        _mockCreateValidator
            .Setup(v => v.ValidateAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _controller.CreateOrder(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        badRequestResult.Value.Should().BeOfType<ValidationProblemDetails>();
        
        var problemDetails = badRequestResult.Value as ValidationProblemDetails;
        problemDetails!.Errors.Should().ContainKey("CustomerId");
        problemDetails.Errors.Should().ContainKey("Items");
    }

    [Fact]
    public async Task CreateOrder_WhenMediatorThrows_ShouldReturnInternalServerError()
    {
        // Arrange
        var command = new CreateOrderCommand(
            "CUST001",
            new List<OrderItemDto> { new("PROD001", 1, 10.00m) });

        _mockCreateValidator
            .Setup(v => v.ValidateAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _mockMediator
            .Setup(m => m.Send(command, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _controller.CreateOrder(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    #endregion

    #region GetOrder Tests

    [Fact]
    public async Task GetOrder_WithExistingId_ShouldReturnOkResult()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var expectedOrder = new OrderResponseDto(
            orderId,
            "CUST001",
            DateTime.UtcNow,
            "Pending",
            20.00m,
            new List<OrderItemResponseDto>
            {
                new(Guid.NewGuid(), "PROD001", 2, 10.00m, 20.00m)
            });

        _mockMediator
            .Setup(m => m.Send(It.Is<GetOrderQuery>(q => q.OrderId == orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedOrder);

        // Act
        var result = await _controller.GetOrder(orderId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().Be(expectedOrder);
    }

    [Fact]
    public async Task GetOrder_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        _mockMediator
            .Setup(m => m.Send(It.Is<GetOrderQuery>(q => q.OrderId == orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderResponseDto?)null);

        // Act
        var result = await _controller.GetOrder(orderId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = result as NotFoundObjectResult;
        notFoundResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetOrder_WhenMediatorThrows_ShouldReturnInternalServerError()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        _mockMediator
            .Setup(m => m.Send(It.IsAny<GetOrderQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _controller.GetOrder(orderId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    #endregion

    #region GetOrders Tests

    [Fact]
    public async Task GetOrders_WithValidParameters_ShouldReturnOkResult()
    {
        // Arrange
        var expectedResult = new PagedOrdersResult(
            new List<OrderResponseDto>
            {
                new(Guid.NewGuid(), "CUST001", DateTime.UtcNow, "Pending", 20.00m, new List<OrderItemResponseDto>())
            },
            TotalCount: 1,
            PageNumber: 1,
            PageSize: 20,
            HasNextPage: false,
            HasPreviousPage: false);

        _mockMediator
            .Setup(m => m.Send(It.IsAny<GetOrdersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.GetOrders(1, 20, "CUST001", CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().Be(expectedResult);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    public async Task GetOrders_WithInvalidPageNumber_ShouldReturnBadRequest(int pageNumber, int pageSize)
    {
        // Act
        var result = await _controller.GetOrders(pageNumber, pageSize, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 101)]
    public async Task GetOrders_WithInvalidPageSize_ShouldReturnBadRequest(int pageNumber, int pageSize)
    {
        // Act
        var result = await _controller.GetOrders(pageNumber, pageSize, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task GetOrders_WhenMediatorThrows_ShouldReturnInternalServerError()
    {
        // Arrange
        _mockMediator
            .Setup(m => m.Send(It.IsAny<GetOrdersQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _controller.GetOrders(1, 20, null, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    #endregion

    #region UpdateOrder Tests

    [Fact]
    public async Task UpdateOrder_WithValidCommand_ShouldReturnOkResult()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new UpdateOrderCommand(
            orderId,
            "CUST001",
            new List<OrderItemDto> { new("PROD001", 2, 10.00m) });

        var expectedResult = new UpdateOrderResult(
            orderId,
            "CUST001",
            20.00m,
            DateTime.UtcNow);

        _mockUpdateValidator
            .Setup(v => v.ValidateAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _mockMediator
            .Setup(m => m.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _controller.UpdateOrder(orderId, command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<OkObjectResult>();
        var okResult = result as OkObjectResult;
        okResult!.StatusCode.Should().Be(StatusCodes.Status200OK);
        okResult.Value.Should().Be(expectedResult);
    }

    [Fact]
    public async Task UpdateOrder_WithMismatchedIds_ShouldReturnBadRequest()
    {
        // Arrange
        var urlId = Guid.NewGuid();
        var commandId = Guid.NewGuid();
        var command = new UpdateOrderCommand(
            commandId,
            "CUST001",
            new List<OrderItemDto> { new("PROD001", 1, 10.00m) });

        // Act
        var result = await _controller.UpdateOrder(urlId, command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult!.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task UpdateOrder_WithInvalidCommand_ShouldReturnBadRequest()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new UpdateOrderCommand(orderId, "", new List<OrderItemDto>());
        
        var validationResult = new ValidationResult(new[]
        {
            new ValidationFailure("CustomerId", "Customer ID is required")
        });

        _mockUpdateValidator
            .Setup(v => v.ValidateAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(validationResult);

        // Act
        var result = await _controller.UpdateOrder(orderId, command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var badRequestResult = result as BadRequestObjectResult;
        badRequestResult!.Value.Should().BeOfType<ValidationProblemDetails>();
    }

    [Fact]
    public async Task UpdateOrder_WithNonExistentOrder_ShouldReturnNotFound()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new UpdateOrderCommand(
            orderId,
            "CUST001",
            new List<OrderItemDto> { new("PROD001", 1, 10.00m) });

        _mockUpdateValidator
            .Setup(v => v.ValidateAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _mockMediator
            .Setup(m => m.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UpdateOrderResult?)null);

        // Act
        var result = await _controller.UpdateOrder(orderId, command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = result as NotFoundObjectResult;
        notFoundResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task UpdateOrder_WhenMediatorThrows_ShouldReturnInternalServerError()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var command = new UpdateOrderCommand(
            orderId,
            "CUST001",
            new List<OrderItemDto> { new("PROD001", 1, 10.00m) });

        _mockUpdateValidator
            .Setup(v => v.ValidateAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        _mockMediator
            .Setup(m => m.Send(command, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _controller.UpdateOrder(orderId, command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    #endregion

    #region DeleteOrder Tests

    [Fact]
    public async Task DeleteOrder_WithExistingOrder_ShouldReturnNoContent()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        _mockMediator
            .Setup(m => m.Send(It.Is<DeleteOrderCommand>(c => c.OrderId == orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _controller.DeleteOrder(orderId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        var noContentResult = result as NoContentResult;
        noContentResult!.StatusCode.Should().Be(StatusCodes.Status204NoContent);
    }

    [Fact]
    public async Task DeleteOrder_WithNonExistentOrder_ShouldReturnNotFound()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        _mockMediator
            .Setup(m => m.Send(It.Is<DeleteOrderCommand>(c => c.OrderId == orderId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _controller.DeleteOrder(orderId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
        var notFoundResult = result as NotFoundObjectResult;
        notFoundResult!.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task DeleteOrder_WhenMediatorThrows_ShouldReturnInternalServerError()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        _mockMediator
            .Setup(m => m.Send(It.IsAny<DeleteOrderCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        // Act
        var result = await _controller.DeleteOrder(orderId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = result as ObjectResult;
        objectResult!.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    #endregion
}