using MediatR;
using Microsoft.Extensions.Logging;
using WorkerService.Application.Queries;
using WorkerService.Domain.Interfaces;

namespace WorkerService.Application.Handlers;

public class GetOrderQueryHandler : IRequestHandler<GetOrderQuery, OrderDto?>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<GetOrderQueryHandler> _logger;

    public GetOrderQueryHandler(
        IOrderRepository orderRepository,
        ILogger<GetOrderQueryHandler> logger)
    {
        _orderRepository = orderRepository;
        _logger = logger;
    }

    public async Task<OrderDto?> Handle(GetOrderQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving order {OrderId}", request.OrderId);

        var order = await _orderRepository.GetOrderWithItemsAsync(request.OrderId, cancellationToken);

        if (order == null)
        {
            _logger.LogWarning("Order {OrderId} not found", request.OrderId);
            return null;
        }

        // Map domain entity to DTO
        var orderDto = new OrderDto(
            order.Id,
            order.CustomerId,
            order.OrderDate,
            order.Status,
            order.TotalAmount.Amount,
            order.Items.Select(item => new Queries.OrderItemDto(
                item.Id,
                item.ProductId,
                item.Quantity,
                item.UnitPrice.Amount,
                item.TotalPrice.Amount)).ToList());

        _logger.LogInformation("Order {OrderId} retrieved successfully", request.OrderId);

        return orderDto;
    }
}