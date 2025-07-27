using MediatR;
using Microsoft.Extensions.Logging;
using WorkerService.Application.Commands;
using WorkerService.Domain.Entities;
using WorkerService.Domain.Interfaces;
using WorkerService.Domain.ValueObjects;

namespace WorkerService.Application.Handlers;

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, CreateOrderResult>
{
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    public CreateOrderCommandHandler(
        IOrderRepository orderRepository,
        ILogger<CreateOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _logger = logger;
    }

    public async Task<CreateOrderResult> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating order for customer {CustomerId}", request.CustomerId);

        // Convert DTOs to domain entities
        var orderItems = request.Items.Select(item => 
            new OrderItem(item.ProductId, item.Quantity, new Money(item.UnitPrice))).ToList();

        // Create domain entity
        var order = new Order(request.CustomerId, orderItems);

        // Persist to database
        await _orderRepository.AddAsync(order, cancellationToken);

        _logger.LogInformation("Order {OrderId} created successfully for customer {CustomerId}", 
            order.Id, request.CustomerId);

        // Return result
        return new CreateOrderResult(
            order.Id,
            order.CustomerId,
            order.TotalAmount.Amount,
            order.OrderDate);
    }
}