using MediatR;
using WorkerService.Domain.Entities;

namespace WorkerService.Application.Queries;

public record GetOrderQuery(Guid OrderId) : IRequest<OrderDto?>;

public record OrderDto(
    Guid Id,
    string CustomerId,
    DateTime OrderDate,
    OrderStatus Status,
    decimal TotalAmount,
    IList<OrderItemDto> Items);

public record OrderItemDto(
    Guid Id,
    string ProductId,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice);