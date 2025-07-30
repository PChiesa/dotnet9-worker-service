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
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(
        IMediator mediator,
        IValidator<CreateOrderCommand> createValidator,
        IValidator<UpdateOrderCommand> updateValidator,
        ILogger<OrdersController> logger)
    {
        _mediator = mediator;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
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
}