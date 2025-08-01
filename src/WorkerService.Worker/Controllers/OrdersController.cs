using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkerService.Application.Commands;
using WorkerService.Application.Common.Extensions;
using WorkerService.Application.Queries;

namespace WorkerService.Worker.Controllers;

/// <summary>
/// RESTful API controller for Order management operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize]
public class OrdersController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IValidator<CreateOrderCommand> _createValidator;
    private readonly IValidator<UpdateOrderCommand> _updateValidator;
    private readonly IValidator<ProcessPaymentCommand> _processPaymentValidator;
    private readonly IValidator<ShipOrderCommand> _shipOrderValidator;
    private readonly IValidator<MarkOrderDeliveredCommand> _markDeliveredValidator;
    private readonly IValidator<CancelOrderCommand> _cancelOrderValidator;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IMediator mediator,
        IValidator<CreateOrderCommand> createValidator,
        IValidator<UpdateOrderCommand> updateValidator,
        IValidator<ProcessPaymentCommand> processPaymentValidator,
        IValidator<ShipOrderCommand> shipOrderValidator,
        IValidator<MarkOrderDeliveredCommand> markDeliveredValidator,
        IValidator<CancelOrderCommand> cancelOrderValidator,
        ILogger<OrdersController> logger)
    {
        _mediator = mediator;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _processPaymentValidator = processPaymentValidator;
        _shipOrderValidator = shipOrderValidator;
        _markDeliveredValidator = markDeliveredValidator;
        _cancelOrderValidator = cancelOrderValidator;
        _logger = logger;
    }

    /// <summary>
    /// Creates a new order
    /// </summary>
    /// <param name="command">Order creation data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created order details</returns>
    [HttpPost]
    [ProducesResponseType<CreateOrderResult>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("API request to create order for customer {CustomerId}", command.CustomerId);

            // Manual validation (recommended pattern for .NET 9)
            var validationResult = await _createValidator.ValidateAsync(command, cancellationToken);
            if (!validationResult.IsValid)
            {
                var problemDetails = new ValidationProblemDetails();
                foreach (var error in validationResult.Errors)
                {
                    problemDetails.Errors.TryAdd(error.PropertyName, new[] { error.ErrorMessage });
                }
                return BadRequest(problemDetails);
            }

            var result = await _mediator.Send(command, cancellationToken);

            _logger.LogInformation("Order {OrderId} created successfully via API", result.OrderId);

            return CreatedAtAction(nameof(GetOrder), new { id = result.OrderId }, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create order for customer {CustomerId}", command.CustomerId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while creating the order" });
        }
    }

    /// <summary>
    /// Gets an order by ID
    /// </summary>
    /// <param name="id">Order ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Order details if found</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetOrder(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            if (id == Guid.Empty)
                throw new ArgumentException("Order ID cannot be empty", nameof(id));

            _logger.LogInformation("API request to get order {OrderId}", id);

            var query = new GetOrderQuery(id);
            var result = await _mediator.Send(query, cancellationToken);

            if (result == null)
            {
                _logger.LogWarning("Order {OrderId} not found via API", id);
                return NotFound(new { message = $"Order with ID {id} was not found" });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve order {OrderId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while retrieving the order" });
        }
    }

    /// <summary>
    /// Gets paginated list of orders with optional filtering
    /// </summary>
    /// <param name="pageNumber">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 20, max: 100)</param>
    /// <param name="customerId">Optional customer ID filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Paginated list of orders</returns>
    [HttpGet]
    [ProducesResponseType<PagedOrdersResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetOrders(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? customerId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate pagination parameters
            if (pageNumber < 1)
            {
                return BadRequest(new { message = "Page number must be greater than 0" });
            }

            if (pageSize < 1 || pageSize > 100)
            {
                return BadRequest(new { message = "Page size must be between 1 and 100" });
            }

            _logger.LogInformation("API request to get orders - Page: {PageNumber}, Size: {PageSize}, Customer: {CustomerId}",
                pageNumber, pageSize, customerId ?? "all");

            var query = new GetOrdersQuery(pageNumber, pageSize, customerId);
            var result = await _mediator.Send(query, cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve orders list");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while retrieving orders" });
        }
    }

    /// <summary>
    /// Updates an existing order
    /// </summary>
    /// <param name="id">Order ID</param>
    /// <param name="command">Order update data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated order details</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<UpdateOrderResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> UpdateOrder(
        Guid id,
        [FromBody] UpdateOrderCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            if (id != command.OrderId)
            {
                return BadRequest(new { message = "Order ID in URL does not match Order ID in request body" });
            }

            _logger.LogInformation("API request to update order {OrderId}", id);

            var validationResult = await _updateValidator.ValidateAsync(command, cancellationToken);
            if (!validationResult.IsValid)
            {
                var problemDetails = new ValidationProblemDetails();
                foreach (var error in validationResult.Errors)
                {
                    problemDetails.Errors.TryAdd(error.PropertyName, new[] { error.ErrorMessage });
                }
                return BadRequest(problemDetails);
            }

            var result = await _mediator.Send(command, cancellationToken);

            if (result == null)
            {
                _logger.LogWarning("Order {OrderId} not found for update via API", id);
                return NotFound(new { message = $"Order with ID {id} was not found" });
            }

            _logger.LogInformation("Order {OrderId} updated successfully via API", id);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update order {OrderId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while updating the order" });
        }
    }

    /// <summary>
    /// Soft deletes an order (marks as cancelled)
    /// </summary>
    /// <param name="id">Order ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content if successful</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteOrder(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("API request to delete order {OrderId}", id);

            var command = new DeleteOrderCommand(id);
            var result = await _mediator.Send(command, cancellationToken);

            if (!result)
            {
                _logger.LogWarning("Order {OrderId} not found for deletion via API", id);
                return NotFound(new { message = $"Order with ID {id} was not found" });
            }

            _logger.LogInformation("Order {OrderId} deleted successfully via API", id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete order {OrderId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while deleting the order" });
        }
    }

    /// <summary>
    /// Process payment for a validated order
    /// </summary>
    /// <param name="id">Order ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success status</returns>
    [HttpPost("{id:guid}/pay")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ProcessPayment(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("API request to process payment for order {OrderId}", id);

            var command = new ProcessPaymentCommand(id);
            var validationResult = await _processPaymentValidator.ValidateAsync(command, cancellationToken);
            if (!validationResult.IsValid)
            {
                var problemDetails = new ValidationProblemDetails();
                foreach (var error in validationResult.Errors)
                {
                    problemDetails.Errors.TryAdd(error.PropertyName, new[] { error.ErrorMessage });
                }
                return BadRequest(problemDetails);
            }

            var result = await _mediator.Send(command, cancellationToken);

            if (!result)
            {
                _logger.LogWarning("Order {OrderId} not found for payment processing via API", id);
                return NotFound(new { message = $"Order with ID {id} was not found" });
            }

            _logger.LogInformation("Payment processed successfully for order {OrderId} via API", id);

            return Ok(new { message = "Payment processed successfully", orderId = id });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation when processing payment for order {OrderId}", id);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process payment for order {OrderId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while processing the payment" });
        }
    }

    /// <summary>
    /// Ship a paid order with tracking number
    /// </summary>
    /// <param name="id">Order ID</param>
    /// <param name="request">Ship order request containing tracking number</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success status</returns>
    [HttpPost("{id:guid}/ship")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ShipOrder(Guid id, [FromBody] ShipOrderRequest request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("API request to ship order {OrderId} with tracking number {TrackingNumber}", 
                id, request.TrackingNumber);

            var command = new ShipOrderCommand(id, request.TrackingNumber);
            var validationResult = await _shipOrderValidator.ValidateAsync(command, cancellationToken);
            if (!validationResult.IsValid)
            {
                var problemDetails = new ValidationProblemDetails();
                foreach (var error in validationResult.Errors)
                {
                    problemDetails.Errors.TryAdd(error.PropertyName, new[] { error.ErrorMessage });
                }
                return BadRequest(problemDetails);
            }

            var result = await _mediator.Send(command, cancellationToken);

            if (!result)
            {
                _logger.LogWarning("Order {OrderId} not found for shipping via API", id);
                return NotFound(new { message = $"Order with ID {id} was not found" });
            }

            _logger.LogInformation("Order {OrderId} shipped successfully via API", id);

            return Ok(new { message = "Order shipped successfully", orderId = id, trackingNumber = request.TrackingNumber });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation when shipping order {OrderId}", id);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ship order {OrderId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while shipping the order" });
        }
    }

    /// <summary>
    /// Mark a shipped order as delivered
    /// </summary>
    /// <param name="id">Order ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success status</returns>
    [HttpPost("{id:guid}/deliver")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> MarkAsDelivered(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("API request to mark order {OrderId} as delivered", id);

            var command = new MarkOrderDeliveredCommand(id);
            var validationResult = await _markDeliveredValidator.ValidateAsync(command, cancellationToken);
            if (!validationResult.IsValid)
            {
                var problemDetails = new ValidationProblemDetails();
                foreach (var error in validationResult.Errors)
                {
                    problemDetails.Errors.TryAdd(error.PropertyName, new[] { error.ErrorMessage });
                }
                return BadRequest(problemDetails);
            }

            var result = await _mediator.Send(command, cancellationToken);

            if (!result)
            {
                _logger.LogWarning("Order {OrderId} not found for delivery marking via API", id);
                return NotFound(new { message = $"Order with ID {id} was not found" });
            }

            _logger.LogInformation("Order {OrderId} marked as delivered successfully via API", id);

            return Ok(new { message = "Order marked as delivered successfully", orderId = id });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation when marking order {OrderId} as delivered", id);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark order {OrderId} as delivered", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while marking the order as delivered" });
        }
    }

    /// <summary>
    /// Cancel an order with optional reason
    /// </summary>
    /// <param name="id">Order ID</param>
    /// <param name="request">Cancel order request containing optional reason</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Success status</returns>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CancelOrder(Guid id, [FromBody] CancelOrderRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("API request to cancel order {OrderId} with reason: {Reason}", 
                id, request?.Reason ?? "No reason provided");

            var command = new CancelOrderCommand(id, request?.Reason);
            var validationResult = await _cancelOrderValidator.ValidateAsync(command, cancellationToken);
            if (!validationResult.IsValid)
            {
                var problemDetails = new ValidationProblemDetails();
                foreach (var error in validationResult.Errors)
                {
                    problemDetails.Errors.TryAdd(error.PropertyName, new[] { error.ErrorMessage });
                }
                return BadRequest(problemDetails);
            }

            var result = await _mediator.Send(command, cancellationToken);

            if (!result)
            {
                _logger.LogWarning("Order {OrderId} not found for cancellation via API", id);
                return NotFound(new { message = $"Order with ID {id} was not found" });
            }

            _logger.LogInformation("Order {OrderId} cancelled successfully via API", id);

            return Ok(new { message = "Order cancelled successfully", orderId = id, reason = request?.Reason });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation when cancelling order {OrderId}", id);
            return Conflict(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel order {OrderId}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "An error occurred while cancelling the order" });
        }
    }
}

/// <summary>
/// Request model for shipping an order
/// </summary>
public record ShipOrderRequest(string TrackingNumber);

/// <summary>
/// Request model for cancelling an order
/// </summary>
public record CancelOrderRequest(string? Reason = null);