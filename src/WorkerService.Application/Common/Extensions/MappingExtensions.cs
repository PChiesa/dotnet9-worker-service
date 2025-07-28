using WorkerService.Application.Commands;
using WorkerService.Application.Queries;
using WorkerService.Domain.Entities;
using WorkerService.Domain.ValueObjects;

namespace WorkerService.Application.Common.Extensions;

/// <summary>
/// Manual mapping extensions for entity-DTO conversion following Clean Architecture principles.
/// Uses static extension methods for efficient, maintainable conversions without external dependencies.
/// </summary>
public static class OrderMappingExtensions
{
    /// <summary>
    /// Converts Order entity to OrderResponseDto for API responses
    /// </summary>
    public static OrderResponseDto ToResponseDto(this Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        
        return new OrderResponseDto(
            Id: order.Id,
            CustomerId: order.CustomerId,
            OrderDate: order.OrderDate,
            Status: order.Status.ToString(),
            TotalAmount: order.TotalAmount.Amount,
            Items: order.Items.Select(item => item.ToResponseDto()));
    }
    
    /// <summary>
    /// Converts OrderItem entity to OrderItemResponseDto for API responses
    /// </summary>
    public static OrderItemResponseDto ToResponseDto(this OrderItem orderItem)
    {
        ArgumentNullException.ThrowIfNull(orderItem);
        
        return new OrderItemResponseDto(
            Id: orderItem.Id,
            ProductId: orderItem.ProductId,
            Quantity: orderItem.Quantity,
            UnitPrice: orderItem.UnitPrice.Amount,
            TotalPrice: orderItem.UnitPrice.Amount * orderItem.Quantity);
    }
    
    /// <summary>
    /// Converts CreateOrderCommand to Order entity for domain operations
    /// </summary>
    public static Order ToEntity(this CreateOrderCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        
        var orderItems = command.Items?.Select(item => 
            new OrderItem(item.ProductId, item.Quantity, new Money(item.UnitPrice))) 
            ?? throw new ArgumentException("Order items cannot be null", nameof(command));
            
        return new Order(command.CustomerId, orderItems);
    }
    
    /// <summary>
    /// Converts Order entity to CreateOrderResult for API responses
    /// </summary>
    public static CreateOrderResult ToCreateResult(this Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        
        return new CreateOrderResult(
            OrderId: order.Id,
            CustomerId: order.CustomerId,
            TotalAmount: order.TotalAmount.Amount,
            OrderDate: order.OrderDate);
    }
    
    /// <summary>
    /// Converts Order entity to UpdateOrderResult for API responses
    /// </summary>
    public static UpdateOrderResult ToUpdateResult(this Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        
        return new UpdateOrderResult(
            OrderId: order.Id,
            CustomerId: order.CustomerId,
            TotalAmount: order.TotalAmount.Amount,
            UpdatedAt: order.UpdatedAt);
    }
    
    /// <summary>
    /// Converts paged repository data to PagedOrdersResult for API responses
    /// </summary>
    public static PagedOrdersResult ToPagedResult(
        this (IEnumerable<Order> Orders, int TotalCount) pagedData,
        int pageNumber,
        int pageSize)
    {
        var (orders, totalCount) = pagedData;
        var orderDtos = orders.Select(o => o.ToResponseDto());
        
        return new PagedOrdersResult(
            Orders: orderDtos,
            TotalCount: totalCount,
            PageNumber: pageNumber,
            PageSize: pageSize,
            HasNextPage: pageNumber * pageSize < totalCount,
            HasPreviousPage: pageNumber > 1);
    }
}

/// <summary>
/// Response DTO for order API operations
/// </summary>
public record OrderResponseDto(
    Guid Id,
    string CustomerId,
    DateTime OrderDate,
    string Status,
    decimal TotalAmount,
    IEnumerable<OrderItemResponseDto> Items);

/// <summary>
/// Response DTO for order item API operations
/// </summary>
public record OrderItemResponseDto(
    Guid Id,
    string ProductId,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice);

/// <summary>
/// Result DTO for order update operations
/// </summary>
public record UpdateOrderResult(
    Guid OrderId,
    string CustomerId,
    decimal TotalAmount,
    DateTime UpdatedAt);

/// <summary>
/// Result DTO for paginated order queries
/// </summary>
public record PagedOrdersResult(
    IEnumerable<OrderResponseDto> Orders,
    int TotalCount,
    int PageNumber,
    int PageSize,
    bool HasNextPage,
    bool HasPreviousPage);