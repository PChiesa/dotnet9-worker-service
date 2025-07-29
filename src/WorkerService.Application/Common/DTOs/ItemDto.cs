namespace WorkerService.Application.Common.DTOs;

public record ItemDto(
    Guid Id,
    string SKU,
    string Name,
    string Description,
    decimal Price,
    string Currency,
    int AvailableStock,
    int ReservedStock,
    string Category,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record CreateItemDto(
    string SKU,
    string Name,
    string Description,
    decimal Price,
    int InitialStock,
    string Category
);

public record UpdateItemDto(
    string Name,
    string Description,
    decimal Price,
    string Category
);

public record StockAdjustmentDto(
    int NewQuantity,
    string Reason
);

public record StockReservationDto(
    int Quantity,
    string OrderId
);

public record PagedItemsResult(
    IEnumerable<ItemDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    bool HasNextPage,
    bool HasPreviousPage
);