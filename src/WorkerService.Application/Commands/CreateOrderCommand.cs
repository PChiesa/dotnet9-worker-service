using MediatR;

namespace WorkerService.Application.Commands;

public record CreateOrderCommand(
    string CustomerId,
    IList<OrderItemDto> Items) : IRequest<CreateOrderResult>;

public record OrderItemDto(
    string ProductId,
    int Quantity,
    decimal UnitPrice);

public record CreateOrderResult(
    Guid OrderId,
    string CustomerId,
    decimal TotalAmount,
    DateTime OrderDate);