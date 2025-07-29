# 🚀 Production-Ready Plan: Item CRUD API with Minimal API Endpoints

## 📋 Executive Summary

This PRP outlines the implementation of a comprehensive Item management system using .NET 9 minimal APIs, following Clean Architecture principles with CQRS pattern. The system will manage product catalog items independently from Orders, enabling centralized inventory management with features including stock tracking, SKU-based identification, and optimistic concurrency control. The implementation strictly adheres to minimal API patterns, replacing controller-based approaches as mandated by the codebase standards.

## 🎯 Success Criteria Checklist

- [ ] Item entity created as aggregate root with proper domain modeling
- [ ] All value objects (SKU, Price, StockLevel) implemented with validation
- [ ] Complete CQRS implementation for all item operations
- [ ] FluentValidation validators for all commands with comprehensive rules
- [ ] All endpoints implemented using minimal APIs (NO controllers)
- [ ] Database migrations create Items table with proper constraints and indexes
- [ ] Unique SKU constraint enforced at database and domain levels
- [ ] Soft delete functionality preserves referential integrity
- [ ] Stock management operations are atomic and prevent race conditions
- [ ] OrderItem updated to reference Item entity by ID
- [ ] Full OpenAPI documentation for all endpoints
- [ ] Pagination implemented with configurable limits
- [ ] Search and filtering capabilities on item list endpoint
- [ ] All endpoints return proper HTTP status codes
- [ ] Domain events published for all state changes
- [ ] Integration tests achieve >90% code coverage
- [ ] Performance: List endpoint returns 1000 items in <100ms
- [ ] Backward compatibility maintained for existing OrderItem.ProductId

## 🏗️ Architecture Overview

### Clean Architecture Layers

```
src/
├── WorkerService.Domain/           # Item entity, value objects, repository interfaces
├── WorkerService.Application/      # CQRS handlers, validators, DTOs
├── WorkerService.Infrastructure/   # EF Core configuration, repository implementation
└── WorkerService.Worker/           # Minimal API endpoints, Program.cs extensions
```

### Key Design Decisions

1. **Aggregate Design**: Item as aggregate root with embedded value objects
2. **REPR Pattern**: Request → Execute → Present → Respond for API endpoints
3. **Optimistic Concurrency**: Version-based concurrency control for stock updates
4. **Soft Delete Pattern**: Maintain referential integrity with historical orders
5. **Feature Folder Structure**: Organize by feature (Items) with subfolders for commands/queries

## 📊 Current State Analysis

### Existing Architecture
- **API Pattern**: Controller-based (OrdersController) - needs migration to minimal APIs
- **Domain Model**: Order aggregate with OrderItem referencing products by string ProductId
- **CQRS**: Already implemented with MediatR for Orders
- **Testing**: Comprehensive integration tests using WebApplicationFactory
- **Database**: Supports both in-memory and PostgreSQL configurations

### Migration Requirements
1. Transform controller patterns to minimal API endpoints
2. Create Item aggregate independent of Order
3. Update OrderItem to reference Items by ID while maintaining backward compatibility
4. Implement stock management with concurrency control

## 🔄 Implementation Phases

### Phase 1: Domain Layer Implementation
**Objective**: Create Item aggregate root with business rules and value objects

#### 1.1 Create Value Objects

```csharp
// File: src/WorkerService.Domain/ValueObjects/SKU.cs
namespace WorkerService.Domain.ValueObjects;

public class SKU : IEquatable<SKU>
{
    public string Value { get; }

    public SKU(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("SKU cannot be empty", nameof(value));
        
        if (value.Length > 50)
            throw new ArgumentException("SKU cannot exceed 50 characters", nameof(value));
        
        if (!System.Text.RegularExpressions.Regex.IsMatch(value, @"^[A-Z0-9\-]+$"))
            throw new ArgumentException("SKU must contain only uppercase letters, numbers, and hyphens", nameof(value));

        Value = value;
    }

    public override bool Equals(object? obj) => Equals(obj as SKU);
    public bool Equals(SKU? other) => other != null && Value == other.Value;
    public override int GetHashCode() => Value.GetHashCode();
    public override string ToString() => Value;
    
    public static implicit operator string(SKU sku) => sku.Value;
    public static explicit operator SKU(string value) => new(value);
}

// File: src/WorkerService.Domain/ValueObjects/Price.cs
namespace WorkerService.Domain.ValueObjects;

public class Price : IEquatable<Price>
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Price(decimal amount, string currency = "USD")
    {
        if (amount < 0)
            throw new ArgumentException("Price cannot be negative", nameof(amount));
        
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency cannot be empty", nameof(currency));

        Amount = Math.Round(amount, 2);
        Currency = currency.ToUpperInvariant();
    }

    public bool Equals(Price? other) => 
        other != null && Amount == other.Amount && Currency == other.Currency;
    
    public override bool Equals(object? obj) => Equals(obj as Price);
    public override int GetHashCode() => HashCode.Combine(Amount, Currency);
    public override string ToString() => $"{Amount:F2} {Currency}";
}

// File: src/WorkerService.Domain/ValueObjects/StockLevel.cs
namespace WorkerService.Domain.ValueObjects;

public class StockLevel : IEquatable<StockLevel>
{
    public int Available { get; private set; }
    public int Reserved { get; private set; }
    public int Total => Available + Reserved;

    public StockLevel(int available, int reserved = 0)
    {
        if (available < 0)
            throw new ArgumentException("Available stock cannot be negative", nameof(available));
        
        if (reserved < 0)
            throw new ArgumentException("Reserved stock cannot be negative", nameof(reserved));

        Available = available;
        Reserved = reserved;
    }

    public StockLevel Reserve(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Reserve quantity must be positive", nameof(quantity));
        
        if (quantity > Available)
            throw new InvalidOperationException($"Cannot reserve {quantity} items. Only {Available} available.");

        return new StockLevel(Available - quantity, Reserved + quantity);
    }

    public StockLevel Release(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Release quantity must be positive", nameof(quantity));
        
        if (quantity > Reserved)
            throw new InvalidOperationException($"Cannot release {quantity} items. Only {Reserved} reserved.");

        return new StockLevel(Available + quantity, Reserved - quantity);
    }

    public StockLevel Adjust(int newAvailable)
    {
        if (newAvailable < 0)
            throw new ArgumentException("Stock level cannot be negative", nameof(newAvailable));

        return new StockLevel(newAvailable, Reserved);
    }

    public StockLevel Commit(int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("Commit quantity must be positive", nameof(quantity));
        
        if (quantity > Reserved)
            throw new InvalidOperationException($"Cannot commit {quantity} items. Only {Reserved} reserved.");

        return new StockLevel(Available, Reserved - quantity);
    }

    public bool Equals(StockLevel? other) => 
        other != null && Available == other.Available && Reserved == other.Reserved;
    
    public override bool Equals(object? obj) => Equals(obj as StockLevel);
    public override int GetHashCode() => HashCode.Combine(Available, Reserved);
    public override string ToString() => $"Available: {Available}, Reserved: {Reserved}";
}
```

#### 1.2 Create Item Aggregate Root

```csharp
// File: src/WorkerService.Domain/Entities/Item.cs
using WorkerService.Domain.ValueObjects;
using WorkerService.Domain.Events;

namespace WorkerService.Domain.Entities;

public class Item
{
    public Guid Id { get; private set; }
    public SKU SKU { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public Price Price { get; private set; }
    public StockLevel StockLevel { get; private set; }
    public string Category { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public byte[] Version { get; private set; } // For optimistic concurrency
    
    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    // EF Core constructor
    private Item() { }

    public Item(SKU sku, string name, string description, Price price, int initialStock, string category)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Item name cannot be empty", nameof(name));
        
        if (name.Length > 200)
            throw new ArgumentException("Item name cannot exceed 200 characters", nameof(name));
        
        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Category cannot be empty", nameof(category));

        Id = Guid.NewGuid();
        SKU = sku ?? throw new ArgumentNullException(nameof(sku));
        Name = name;
        Description = description ?? string.Empty;
        Price = price ?? throw new ArgumentNullException(nameof(price));
        StockLevel = new StockLevel(initialStock);
        Category = category;
        IsActive = true;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        Version = Guid.NewGuid().ToByteArray();

        _domainEvents.Add(new ItemCreatedEvent(Id, SKU.Value, Name, Price.Amount));
    }

    public void Update(string name, string description, Price price, string category)
    {
        if (!IsActive)
            throw new InvalidOperationException("Cannot update inactive item");

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Item name cannot be empty", nameof(name));
        
        if (name.Length > 200)
            throw new ArgumentException("Item name cannot exceed 200 characters", nameof(name));

        var hasChanges = Name != name || 
                        Description != description || 
                        !Price.Equals(price) || 
                        Category != category;

        if (hasChanges)
        {
            Name = name;
            Description = description ?? string.Empty;
            Price = price ?? throw new ArgumentNullException(nameof(price));
            Category = category ?? throw new ArgumentNullException(nameof(category));
            UpdatedAt = DateTime.UtcNow;
            Version = Guid.NewGuid().ToByteArray();

            _domainEvents.Add(new ItemUpdatedEvent(Id, SKU.Value, Name, Price.Amount));
        }
    }

    public void AdjustStock(int newQuantity)
    {
        if (!IsActive)
            throw new InvalidOperationException("Cannot adjust stock for inactive item");

        var oldQuantity = StockLevel.Available;
        StockLevel = StockLevel.Adjust(newQuantity);
        UpdatedAt = DateTime.UtcNow;
        Version = Guid.NewGuid().ToByteArray();

        _domainEvents.Add(new StockAdjustedEvent(Id, SKU.Value, oldQuantity, newQuantity));
    }

    public void ReserveStock(int quantity)
    {
        if (!IsActive)
            throw new InvalidOperationException("Cannot reserve stock for inactive item");

        StockLevel = StockLevel.Reserve(quantity);
        UpdatedAt = DateTime.UtcNow;
        Version = Guid.NewGuid().ToByteArray();

        _domainEvents.Add(new StockReservedEvent(Id, SKU.Value, quantity));
    }

    public void ReleaseStock(int quantity)
    {
        StockLevel = StockLevel.Release(quantity);
        UpdatedAt = DateTime.UtcNow;
        Version = Guid.NewGuid().ToByteArray();

        _domainEvents.Add(new StockReleasedEvent(Id, SKU.Value, quantity));
    }

    public void CommitStock(int quantity)
    {
        StockLevel = StockLevel.Commit(quantity);
        UpdatedAt = DateTime.UtcNow;
        Version = Guid.NewGuid().ToByteArray();

        _domainEvents.Add(new StockCommittedEvent(Id, SKU.Value, quantity));
    }

    public void Deactivate()
    {
        if (!IsActive)
            return;

        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
        Version = Guid.NewGuid().ToByteArray();

        _domainEvents.Add(new ItemDeactivatedEvent(Id, SKU.Value));
    }

    public void Activate()
    {
        if (IsActive)
            return;

        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
        Version = Guid.NewGuid().ToByteArray();

        _domainEvents.Add(new ItemActivatedEvent(Id, SKU.Value));
    }

    public void ClearDomainEvents()
    {
        _domainEvents.Clear();
    }
}
```

#### 1.3 Create Domain Events

```csharp
// File: src/WorkerService.Domain/Events/ItemEvents.cs
namespace WorkerService.Domain.Events;

public record ItemCreatedEvent(Guid ItemId, string SKU, string Name, decimal Price) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

public record ItemUpdatedEvent(Guid ItemId, string SKU, string Name, decimal Price) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

public record ItemDeactivatedEvent(Guid ItemId, string SKU) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

public record ItemActivatedEvent(Guid ItemId, string SKU) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

public record StockAdjustedEvent(Guid ItemId, string SKU, int OldQuantity, int NewQuantity) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

public record StockReservedEvent(Guid ItemId, string SKU, int Quantity) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

public record StockReleasedEvent(Guid ItemId, string SKU, int Quantity) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

public record StockCommittedEvent(Guid ItemId, string SKU, int Quantity) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
```

#### 1.4 Create Repository Interface

```csharp
// File: src/WorkerService.Domain/Interfaces/IItemRepository.cs
namespace WorkerService.Domain.Interfaces;

public interface IItemRepository : IRepository<Item>
{
    Task<Item?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);
    Task<bool> SkuExistsAsync(string sku, CancellationToken cancellationToken = default);
    Task<(IEnumerable<Item> Items, int TotalCount)> GetPagedAsync(
        int pageNumber, 
        int pageSize, 
        string? category = null,
        bool? isActive = null,
        string? searchTerm = null,
        CancellationToken cancellationToken = default);
}
```

### Phase 2: Application Layer Implementation
**Objective**: Implement CQRS handlers, DTOs, and validators

#### 2.1 Create DTOs

```csharp
// File: src/WorkerService.Application/Common/DTOs/ItemDto.cs
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
```

#### 2.2 Create Commands

```csharp
// File: src/WorkerService.Application/Commands/CreateItemCommand.cs
using MediatR;
using WorkerService.Application.Common.DTOs;

namespace WorkerService.Application.Commands;

public record CreateItemCommand(
    string SKU,
    string Name,
    string Description,
    decimal Price,
    int InitialStock,
    string Category
) : IRequest<ItemDto>;

// File: src/WorkerService.Application/Commands/UpdateItemCommand.cs
public record UpdateItemCommand(
    Guid ItemId,
    string Name,
    string Description,
    decimal Price,
    string Category
) : IRequest<ItemDto?>;

// File: src/WorkerService.Application/Commands/DeactivateItemCommand.cs
public record DeactivateItemCommand(Guid ItemId) : IRequest<bool>;

// File: src/WorkerService.Application/Commands/AdjustStockCommand.cs
public record AdjustStockCommand(
    Guid ItemId,
    int NewQuantity,
    string Reason
) : IRequest<ItemDto?>;

// File: src/WorkerService.Application/Commands/ReserveStockCommand.cs
public record ReserveStockCommand(
    Guid ItemId,
    int Quantity,
    string OrderId
) : IRequest<ItemDto?>;
```

#### 2.3 Create Queries

```csharp
// File: src/WorkerService.Application/Queries/GetItemQuery.cs
using MediatR;
using WorkerService.Application.Common.DTOs;

namespace WorkerService.Application.Queries;

public record GetItemQuery(Guid ItemId) : IRequest<ItemDto?>;

// File: src/WorkerService.Application/Queries/GetItemBySkuQuery.cs
public record GetItemBySkuQuery(string SKU) : IRequest<ItemDto?>;

// File: src/WorkerService.Application/Queries/GetItemsQuery.cs
public record GetItemsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Category = null,
    bool? IsActive = null,
    string? SearchTerm = null
) : IRequest<PagedItemsResult>;
```

#### 2.4 Create Command Handlers

```csharp
// File: src/WorkerService.Application/Handlers/CreateItemCommandHandler.cs
using MediatR;
using Microsoft.Extensions.Logging;
using WorkerService.Application.Commands;
using WorkerService.Application.Common.DTOs;
using WorkerService.Application.Common.Extensions;
using WorkerService.Domain.Entities;
using WorkerService.Domain.Interfaces;
using WorkerService.Domain.ValueObjects;

namespace WorkerService.Application.Handlers;

public class CreateItemCommandHandler : IRequestHandler<CreateItemCommand, ItemDto>
{
    private readonly IItemRepository _repository;
    private readonly ILogger<CreateItemCommandHandler> _logger;

    public CreateItemCommandHandler(IItemRepository repository, ILogger<CreateItemCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ItemDto> Handle(CreateItemCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating item with SKU {SKU}", request.SKU);

        // Check if SKU already exists
        if (await _repository.SkuExistsAsync(request.SKU, cancellationToken))
        {
            throw new InvalidOperationException($"Item with SKU '{request.SKU}' already exists");
        }

        var sku = new SKU(request.SKU);
        var price = new Price(request.Price);
        
        var item = new Item(
            sku,
            request.Name,
            request.Description,
            price,
            request.InitialStock,
            request.Category
        );

        await _repository.AddAsync(item, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Item {ItemId} with SKU {SKU} created successfully", item.Id, request.SKU);

        return item.ToDto();
    }
}

// File: src/WorkerService.Application/Handlers/UpdateItemCommandHandler.cs
public class UpdateItemCommandHandler : IRequestHandler<UpdateItemCommand, ItemDto?>
{
    private readonly IItemRepository _repository;
    private readonly ILogger<UpdateItemCommandHandler> _logger;

    public UpdateItemCommandHandler(IItemRepository repository, ILogger<UpdateItemCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ItemDto?> Handle(UpdateItemCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating item {ItemId}", request.ItemId);

        var item = await _repository.GetByIdAsync(request.ItemId, cancellationToken);
        if (item == null)
        {
            _logger.LogWarning("Item {ItemId} not found", request.ItemId);
            return null;
        }

        var price = new Price(request.Price);
        item.Update(request.Name, request.Description, price, request.Category);

        await _repository.UpdateAsync(item, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Item {ItemId} updated successfully", request.ItemId);

        return item.ToDto();
    }
}

// File: src/WorkerService.Application/Handlers/AdjustStockCommandHandler.cs
public class AdjustStockCommandHandler : IRequestHandler<AdjustStockCommand, ItemDto?>
{
    private readonly IItemRepository _repository;
    private readonly ILogger<AdjustStockCommandHandler> _logger;

    public AdjustStockCommandHandler(IItemRepository repository, ILogger<AdjustStockCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ItemDto?> Handle(AdjustStockCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Adjusting stock for item {ItemId} to {Quantity}", 
            request.ItemId, request.NewQuantity);

        var item = await _repository.GetByIdAsync(request.ItemId, cancellationToken);
        if (item == null)
        {
            _logger.LogWarning("Item {ItemId} not found", request.ItemId);
            return null;
        }

        item.AdjustStock(request.NewQuantity);

        try
        {
            await _repository.UpdateAsync(item, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning("Concurrent update detected for item {ItemId}", request.ItemId);
            throw new InvalidOperationException("The item was modified by another user. Please refresh and try again.");
        }

        _logger.LogInformation("Stock adjusted for item {ItemId}", request.ItemId);

        return item.ToDto();
    }
}
```

#### 2.5 Create Query Handlers

```csharp
// File: src/WorkerService.Application/Handlers/GetItemQueryHandler.cs
using MediatR;
using Microsoft.Extensions.Logging;
using WorkerService.Application.Common.DTOs;
using WorkerService.Application.Common.Extensions;
using WorkerService.Application.Queries;
using WorkerService.Domain.Interfaces;

namespace WorkerService.Application.Handlers;

public class GetItemQueryHandler : IRequestHandler<GetItemQuery, ItemDto?>
{
    private readonly IItemRepository _repository;
    private readonly ILogger<GetItemQueryHandler> _logger;

    public GetItemQueryHandler(IItemRepository repository, ILogger<GetItemQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ItemDto?> Handle(GetItemQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting item {ItemId}", request.ItemId);

        var item = await _repository.GetByIdAsync(request.ItemId, cancellationToken);
        
        return item?.ToDto();
    }
}

// File: src/WorkerService.Application/Handlers/GetItemBySkuQueryHandler.cs
public class GetItemBySkuQueryHandler : IRequestHandler<GetItemBySkuQuery, ItemDto?>
{
    private readonly IItemRepository _repository;
    private readonly ILogger<GetItemBySkuQueryHandler> _logger;

    public GetItemBySkuQueryHandler(IItemRepository repository, ILogger<GetItemBySkuQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ItemDto?> Handle(GetItemBySkuQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting item by SKU {SKU}", request.SKU);

        var item = await _repository.GetBySkuAsync(request.SKU, cancellationToken);
        
        return item?.ToDto();
    }
}

// File: src/WorkerService.Application/Handlers/GetItemsQueryHandler.cs
public class GetItemsQueryHandler : IRequestHandler<GetItemsQuery, PagedItemsResult>
{
    private readonly IItemRepository _repository;
    private readonly ILogger<GetItemsQueryHandler> _logger;

    public GetItemsQueryHandler(IItemRepository repository, ILogger<GetItemsQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<PagedItemsResult> Handle(GetItemsQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting items - Page: {Page}, Size: {Size}", 
            request.PageNumber, request.PageSize);

        var (items, totalCount) = await _repository.GetPagedAsync(
            request.PageNumber,
            request.PageSize,
            request.Category,
            request.IsActive,
            request.SearchTerm,
            cancellationToken
        );

        var itemDtos = items.Select(i => i.ToDto());

        return new PagedItemsResult(
            itemDtos,
            totalCount,
            request.PageNumber,
            request.PageSize,
            request.PageNumber * request.PageSize < totalCount,
            request.PageNumber > 1
        );
    }
}
```

#### 2.6 Create Validators

```csharp
// File: src/WorkerService.Application/Validators/CreateItemCommandValidator.cs
using FluentValidation;
using WorkerService.Application.Commands;

namespace WorkerService.Application.Validators;

public class CreateItemCommandValidator : AbstractValidator<CreateItemCommand>
{
    public CreateItemCommandValidator()
    {
        RuleFor(x => x.SKU)
            .NotEmpty().WithMessage("SKU is required")
            .MaximumLength(50).WithMessage("SKU cannot exceed 50 characters")
            .Matches(@"^[A-Z0-9\-]+$").WithMessage("SKU must contain only uppercase letters, numbers, and hyphens");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(200).WithMessage("Name cannot exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than zero")
            .PrecisionScale(10, 2, true).WithMessage("Price cannot have more than 2 decimal places");

        RuleFor(x => x.InitialStock)
            .GreaterThanOrEqualTo(0).WithMessage("Initial stock cannot be negative");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Category is required")
            .MaximumLength(100).WithMessage("Category cannot exceed 100 characters");
    }
}

// File: src/WorkerService.Application/Validators/UpdateItemCommandValidator.cs
public class UpdateItemCommandValidator : AbstractValidator<UpdateItemCommand>
{
    public UpdateItemCommandValidator()
    {
        RuleFor(x => x.ItemId)
            .NotEmpty().WithMessage("Item ID is required");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required")
            .MaximumLength(200).WithMessage("Name cannot exceed 200 characters");

        RuleFor(x => x.Description)
            .MaximumLength(1000).WithMessage("Description cannot exceed 1000 characters");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than zero")
            .PrecisionScale(10, 2, true).WithMessage("Price cannot have more than 2 decimal places");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Category is required")
            .MaximumLength(100).WithMessage("Category cannot exceed 100 characters");
    }
}

// File: src/WorkerService.Application/Validators/AdjustStockCommandValidator.cs
public class AdjustStockCommandValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockCommandValidator()
    {
        RuleFor(x => x.ItemId)
            .NotEmpty().WithMessage("Item ID is required");

        RuleFor(x => x.NewQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("Stock quantity cannot be negative");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason for adjustment is required")
            .MaximumLength(500).WithMessage("Reason cannot exceed 500 characters");
    }
}
```

#### 2.7 Create Mapping Extensions

```csharp
// File: src/WorkerService.Application/Common/Extensions/ItemMappingExtensions.cs
using WorkerService.Application.Common.DTOs;
using WorkerService.Domain.Entities;

namespace WorkerService.Application.Common.Extensions;

public static class ItemMappingExtensions
{
    public static ItemDto ToDto(this Item item)
    {
        return new ItemDto(
            item.Id,
            item.SKU.Value,
            item.Name,
            item.Description,
            item.Price.Amount,
            item.Price.Currency,
            item.StockLevel.Available,
            item.StockLevel.Reserved,
            item.Category,
            item.IsActive,
            item.CreatedAt,
            item.UpdatedAt
        );
    }
}
```

### Phase 3: Infrastructure Layer Implementation
**Objective**: Configure EF Core, implement repository, and create migrations

#### 3.1 Configure Entity Type

```csharp
// File: src/WorkerService.Infrastructure/Data/Configurations/ItemConfiguration.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WorkerService.Domain.Entities;
using WorkerService.Domain.ValueObjects;

namespace WorkerService.Infrastructure.Data.Configurations;

public class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("Items");
        
        builder.HasKey(i => i.Id);
        
        builder.Property(i => i.Id)
            .ValueGeneratedNever();
        
        // Configure SKU as owned type
        builder.OwnsOne(i => i.SKU, sku =>
        {
            sku.Property(s => s.Value)
                .HasColumnName("SKU")
                .HasMaxLength(50)
                .IsRequired();
            
            sku.HasIndex(s => s.Value)
                .IsUnique()
                .HasDatabaseName("IX_Items_SKU");
        });
        
        builder.Property(i => i.Name)
            .HasMaxLength(200)
            .IsRequired();
        
        builder.Property(i => i.Description)
            .HasMaxLength(1000);
        
        // Configure Price as owned type
        builder.OwnsOne(i => i.Price, price =>
        {
            price.Property(p => p.Amount)
                .HasColumnName("Price")
                .HasPrecision(10, 2)
                .IsRequired();
            
            price.Property(p => p.Currency)
                .HasColumnName("Currency")
                .HasMaxLength(3)
                .HasDefaultValue("USD")
                .IsRequired();
        });
        
        // Configure StockLevel as owned type
        builder.OwnsOne(i => i.StockLevel, stock =>
        {
            stock.Property(s => s.Available)
                .HasColumnName("AvailableStock")
                .IsRequired();
            
            stock.Property(s => s.Reserved)
                .HasColumnName("ReservedStock")
                .IsRequired();
        });
        
        builder.Property(i => i.Category)
            .HasMaxLength(100)
            .IsRequired();
        
        builder.Property(i => i.IsActive)
            .IsRequired();
        
        builder.Property(i => i.CreatedAt)
            .IsRequired();
        
        builder.Property(i => i.UpdatedAt)
            .IsRequired();
        
        // Configure optimistic concurrency
        builder.Property(i => i.Version)
            .IsConcurrencyToken()
            .ValueGeneratedOnAddOrUpdate();
        
        // Indexes
        builder.HasIndex(i => i.Category)
            .HasDatabaseName("IX_Items_Category");
        
        builder.HasIndex(i => i.IsActive)
            .HasDatabaseName("IX_Items_IsActive");
        
        builder.HasIndex(i => new { i.Category, i.IsActive })
            .HasDatabaseName("IX_Items_Category_IsActive");
        
        // Ignore domain events
        builder.Ignore(i => i.DomainEvents);
    }
}
```

#### 3.2 Update ApplicationDbContext

```csharp
// Add to src/WorkerService.Infrastructure/Data/ApplicationDbContext.cs

public DbSet<Item> Items { get; set; }

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    
    modelBuilder.ApplyConfiguration(new OrderConfiguration());
    modelBuilder.ApplyConfiguration(new OrderItemConfiguration());
    modelBuilder.ApplyConfiguration(new ItemConfiguration()); // Add this line
}
```

#### 3.3 Implement Item Repository

```csharp
// File: src/WorkerService.Infrastructure/Repositories/ItemRepository.cs
using Microsoft.EntityFrameworkCore;
using WorkerService.Domain.Entities;
using WorkerService.Domain.Interfaces;
using WorkerService.Infrastructure.Data;

namespace WorkerService.Infrastructure.Repositories;

public class ItemRepository : Repository<Item>, IItemRepository
{
    private readonly ApplicationDbContext _context;

    public ItemRepository(ApplicationDbContext context) : base(context)
    {
        _context = context;
    }

    public async Task<Item?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        return await _context.Items
            .FirstOrDefaultAsync(i => i.SKU.Value == sku, cancellationToken);
    }

    public async Task<bool> SkuExistsAsync(string sku, CancellationToken cancellationToken = default)
    {
        return await _context.Items
            .AnyAsync(i => i.SKU.Value == sku, cancellationToken);
    }

    public async Task<(IEnumerable<Item> Items, int TotalCount)> GetPagedAsync(
        int pageNumber, 
        int pageSize, 
        string? category = null,
        bool? isActive = null,
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Items.AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(i => i.Category == category);
        }

        if (isActive.HasValue)
        {
            query = query.Where(i => i.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var searchLower = searchTerm.ToLower();
            query = query.Where(i => 
                i.Name.ToLower().Contains(searchLower) || 
                i.SKU.Value.ToLower().Contains(searchLower) ||
                i.Description.ToLower().Contains(searchLower));
        }

        // Get total count
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination
        var items = await query
            .OrderBy(i => i.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
```

### Phase 4: Minimal API Implementation
**Objective**: Create minimal API endpoints following REPR pattern

#### 4.1 Create Item Endpoints

```csharp
// File: src/WorkerService.Worker/Endpoints/ItemEndpoints.cs
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http.HttpResults;
using WorkerService.Application.Commands;
using WorkerService.Application.Common.DTOs;
using WorkerService.Application.Queries;

namespace WorkerService.Worker.Endpoints;

public static class ItemEndpoints
{
    public static RouteGroupBuilder MapItemEndpoints(this RouteGroupBuilder group)
    {
        // POST /api/items
        group.MapPost("/", CreateItem)
            .WithName("CreateItem")
            .WithSummary("Create a new item")
            .WithDescription("Creates a new item in the product catalog")
            .Produces<ItemDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        // GET /api/items/{id}
        group.MapGet("/{id:guid}", GetItem)
            .WithName("GetItem")
            .WithSummary("Get item by ID")
            .WithDescription("Retrieves a specific item by its ID")
            .Produces<ItemDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // GET /api/items
        group.MapGet("/", GetItems)
            .WithName("GetItems")
            .WithSummary("Get paginated list of items")
            .WithDescription("Retrieves a paginated list of items with optional filtering")
            .Produces<PagedItemsResult>();

        // GET /api/items/sku/{sku}
        group.MapGet("/sku/{sku}", GetItemBySku)
            .WithName("GetItemBySku")
            .WithSummary("Get item by SKU")
            .WithDescription("Retrieves a specific item by its SKU")
            .Produces<ItemDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        // PUT /api/items/{id}
        group.MapPut("/{id:guid}", UpdateItem)
            .WithName("UpdateItem")
            .WithSummary("Update an item")
            .WithDescription("Updates an existing item's details")
            .Produces<ItemDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // DELETE /api/items/{id}
        group.MapDelete("/{id:guid}", DeactivateItem)
            .WithName("DeactivateItem")
            .WithSummary("Deactivate an item")
            .WithDescription("Soft deletes an item by marking it as inactive")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        // PUT /api/items/{id}/stock
        group.MapPut("/{id:guid}/stock", AdjustStock)
            .WithName("AdjustStock")
            .WithSummary("Adjust item stock")
            .WithDescription("Updates the available stock quantity for an item")
            .Produces<ItemDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        // POST /api/items/{id}/reserve-stock
        group.MapPost("/{id:guid}/reserve-stock", ReserveStock)
            .WithName("ReserveStock")
            .WithSummary("Reserve item stock")
            .WithDescription("Reserves stock for a pending order")
            .Produces<ItemDto>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return group;
    }

    private static async Task<Results<Created<ItemDto>, ValidationProblem, Conflict<ProblemDetails>>> CreateItem(
        CreateItemDto dto,
        IValidator<CreateItemCommand> validator,
        IMediator mediator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var command = new CreateItemCommand(
            dto.SKU,
            dto.Name,
            dto.Description,
            dto.Price,
            dto.InitialStock,
            dto.Category
        );

        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        try
        {
            var result = await mediator.Send(command, cancellationToken);
            var location = $"{httpContext.Request.Path}/{result.Id}";
            return TypedResults.Created(location, result);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
        {
            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = ex.Message
            };
            return TypedResults.Conflict(problemDetails);
        }
    }

    private static async Task<Results<Ok<ItemDto>, NotFound>> GetItem(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetItemQuery(id);
        var result = await mediator.Send(query, cancellationToken);

        return result != null 
            ? TypedResults.Ok(result) 
            : TypedResults.NotFound();
    }

    private static async Task<Ok<PagedItemsResult>> GetItems(
        [AsParameters] ItemQueryParameters parameters,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetItemsQuery(
            parameters.PageNumber ?? 1,
            parameters.PageSize ?? 20,
            parameters.Category,
            parameters.IsActive,
            parameters.Search
        );

        var result = await mediator.Send(query, cancellationToken);
        return TypedResults.Ok(result);
    }

    private static async Task<Results<Ok<ItemDto>, NotFound>> GetItemBySku(
        string sku,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var query = new GetItemBySkuQuery(sku);
        var result = await mediator.Send(query, cancellationToken);

        return result != null 
            ? TypedResults.Ok(result) 
            : TypedResults.NotFound();
    }

    private static async Task<Results<Ok<ItemDto>, ValidationProblem, NotFound, Conflict<ProblemDetails>>> UpdateItem(
        Guid id,
        UpdateItemDto dto,
        IValidator<UpdateItemCommand> validator,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new UpdateItemCommand(
            id,
            dto.Name,
            dto.Description,
            dto.Price,
            dto.Category
        );

        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        try
        {
            var result = await mediator.Send(command, cancellationToken);
            return result != null 
                ? TypedResults.Ok(result) 
                : TypedResults.NotFound();
        }
        catch (DbUpdateConcurrencyException)
        {
            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = "The item was modified by another user. Please refresh and try again."
            };
            return TypedResults.Conflict(problemDetails);
        }
    }

    private static async Task<Results<NoContent, NotFound>> DeactivateItem(
        Guid id,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new DeactivateItemCommand(id);
        var result = await mediator.Send(command, cancellationToken);

        return result 
            ? TypedResults.NoContent() 
            : TypedResults.NotFound();
    }

    private static async Task<Results<Ok<ItemDto>, ValidationProblem, NotFound, Conflict<ProblemDetails>>> AdjustStock(
        Guid id,
        StockAdjustmentDto dto,
        IValidator<AdjustStockCommand> validator,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new AdjustStockCommand(id, dto.NewQuantity, dto.Reason);

        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        try
        {
            var result = await mediator.Send(command, cancellationToken);
            return result != null 
                ? TypedResults.Ok(result) 
                : TypedResults.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = ex.Message
            };
            return TypedResults.Conflict(problemDetails);
        }
    }

    private static async Task<Results<Ok<ItemDto>, ValidationProblem, NotFound, Conflict<ProblemDetails>>> ReserveStock(
        Guid id,
        StockReservationDto dto,
        IValidator<ReserveStockCommand> validator,
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var command = new ReserveStockCommand(id, dto.Quantity, dto.OrderId);

        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(validationResult.ToDictionary());
        }

        try
        {
            var result = await mediator.Send(command, cancellationToken);
            return result != null 
                ? TypedResults.Ok(result) 
                : TypedResults.NotFound();
        }
        catch (InvalidOperationException ex)
        {
            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = ex.Message
            };
            return TypedResults.Conflict(problemDetails);
        }
    }
}

// Query parameters record
public record ItemQueryParameters(
    int? PageNumber,
    int? PageSize,
    string? Category,
    bool? IsActive,
    string? Search
);
```

#### 4.2 Update Program.cs

```csharp
// Add to Program.cs after existing repository registrations
builder.Services.AddScoped<IItemRepository, ItemRepository>();

// Add after app.MapControllers(); line
// Map Item API endpoints using minimal APIs
app.MapGroup("/api/items")
    .WithTags("Items")
    .WithOpenApi()
    .MapItemEndpoints();
```

### Phase 5: Database Migration and OrderItem Update
**Objective**: Create database migrations and update OrderItem to reference Items

#### 5.1 Create Migration for Items Table

```bash
# Run in Package Manager Console or CLI
dotnet ef migrations add AddItemsTable -c ApplicationDbContext -p src/WorkerService.Infrastructure -s src/WorkerService.Worker
```

#### 5.2 Update OrderItem Entity

```csharp
// Update src/WorkerService.Domain/Entities/OrderItem.cs
public class OrderItem
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public string ProductId { get; private set; } // Keep for backward compatibility
    public Guid? ItemId { get; private set; } // New reference to Item
    public int Quantity { get; private set; }
    public Money UnitPrice { get; private set; }
    public Money TotalPrice => new Money(UnitPrice.Amount * Quantity);

    // Navigation property
    public Item? Item { get; private set; }

    public OrderItem() { } // EF Core constructor

    // Existing constructor for backward compatibility
    public OrderItem(string productId, int quantity, Money unitPrice)
    {
        if (string.IsNullOrWhiteSpace(productId))
            throw new ArgumentException("Product ID cannot be empty", nameof(productId));
        
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero", nameof(quantity));
        
        if (unitPrice.Amount <= 0)
            throw new ArgumentException("Unit price must be greater than zero", nameof(unitPrice));

        Id = Guid.NewGuid();
        ProductId = productId;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    // New constructor with Item reference
    public OrderItem(Guid itemId, string productId, int quantity, Money unitPrice)
    {
        if (itemId == Guid.Empty)
            throw new ArgumentException("Item ID cannot be empty", nameof(itemId));
        
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero", nameof(quantity));
        
        if (unitPrice.Amount <= 0)
            throw new ArgumentException("Unit price must be greater than zero", nameof(unitPrice));

        Id = Guid.NewGuid();
        ItemId = itemId;
        ProductId = productId; // Keep for backward compatibility
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public void UpdateQuantity(int newQuantity)
    {
        if (newQuantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero", nameof(newQuantity));

        Quantity = newQuantity;
    }

    public void LinkToItem(Guid itemId)
    {
        ItemId = itemId;
    }
}
```

#### 5.3 Update OrderItem Configuration

```csharp
// Add to OrderItemConfiguration.cs
builder.Property(oi => oi.ItemId)
    .IsRequired(false);

builder.HasOne(oi => oi.Item)
    .WithMany()
    .HasForeignKey(oi => oi.ItemId)
    .OnDelete(DeleteBehavior.Restrict);

builder.HasIndex(oi => oi.ItemId)
    .HasDatabaseName("IX_OrderItems_ItemId");
```

### Phase 6: Integration Testing
**Objective**: Create comprehensive integration tests for all endpoints

#### 6.1 Create Item API Integration Tests

```csharp
// File: tests/WorkerService.IntegrationTests/InMemory/Tests/ItemsApiInMemoryTests.cs
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using WorkerService.Application.Common.DTOs;
using WorkerService.IntegrationTests.InMemory.Fixtures;
using WorkerService.IntegrationTests.Shared.Utilities;
using Xunit;

namespace WorkerService.IntegrationTests.InMemory.Tests;

[Collection("InMemory Integration Tests")]
public class ItemsApiInMemoryTests : IClassFixture<InMemoryWebApplicationFactory>, IAsyncDisposable
{
    private readonly InMemoryWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public ItemsApiInMemoryTests(InMemoryWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    #region POST /api/items Tests

    [Fact]
    public async Task CreateItem_WithValidData_ShouldReturnCreatedWithLocation()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var createDto = new CreateItemDto(
            SKU: "TEST-001",
            Name: "Test Product",
            Description: "Test Description",
            Price: 99.99m,
            InitialStock: 100,
            Category: "Electronics"
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/items", createDto, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();
        
        var result = await response.Content.ReadFromJsonAsync<ItemDto>(_jsonOptions);
        result.Should().NotBeNull();
        result!.SKU.Should().Be(createDto.SKU);
        result.Name.Should().Be(createDto.Name);
        result.AvailableStock.Should().Be(createDto.InitialStock);
        result.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateItem_WithDuplicateSKU_ShouldReturnConflict()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var createDto = new CreateItemDto(
            SKU: "DUP-001",
            Name: "Duplicate Product",
            Description: "Test",
            Price: 50m,
            InitialStock: 10,
            Category: "Test"
        );

        // Create first item
        await _client.PostAsJsonAsync("/api/items", createDto, _jsonOptions);

        // Act - Try to create duplicate
        var response = await _client.PostAsJsonAsync("/api/items", createDto, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problemDetails.Should().NotBeNull();
        problemDetails!.Detail.Should().Contain("already exists");
    }

    [Theory]
    [InlineData("", "Name is required")]
    [InlineData("INVALID-SKU!", "must contain only uppercase letters")]
    [InlineData("lowercase-sku", "must contain only uppercase letters")]
    public async Task CreateItem_WithInvalidSKU_ShouldReturnBadRequest(string sku, string expectedError)
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var createDto = new CreateItemDto(
            SKU: sku,
            Name: "Test Product",
            Description: "Test",
            Price: 10m,
            InitialStock: 1,
            Category: "Test"
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/items", createDto, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(expectedError);
    }

    #endregion

    #region GET /api/items/{id} Tests

    [Fact]
    public async Task GetItem_WithExistingId_ShouldReturnItem()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var item = await CreateTestItemAsync();

        // Act
        var response = await _client.GetAsync($"/api/items/{item.Id}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<ItemDto>(_jsonOptions);
        result.Should().NotBeNull();
        result!.Id.Should().Be(item.Id);
        result.SKU.Should().Be(item.SKU);
    }

    [Fact]
    public async Task GetItem_WithNonExistentId_ShouldReturnNotFound()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/items/{nonExistentId}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region GET /api/items/sku/{sku} Tests

    [Fact]
    public async Task GetItemBySku_WithExistingSku_ShouldReturnItem()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var item = await CreateTestItemAsync();

        // Act
        var response = await _client.GetAsync($"/api/items/sku/{item.SKU}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<ItemDto>(_jsonOptions);
        result.Should().NotBeNull();
        result!.SKU.Should().Be(item.SKU);
    }

    #endregion

    #region GET /api/items Tests

    [Fact]
    public async Task GetItems_WithPagination_ShouldReturnPagedResults()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        await CreateMultipleTestItemsAsync(25);

        // Act
        var response = await _client.GetAsync("/api/items?pageNumber=2&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<PagedItemsResult>(_jsonOptions);
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(10);
        result.TotalCount.Should().Be(25);
        result.PageNumber.Should().Be(2);
        result.HasNextPage.Should().BeTrue();
        result.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public async Task GetItems_WithCategoryFilter_ShouldReturnFilteredResults()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        await CreateTestItemAsync(sku: "ELEC-001", category: "Electronics");
        await CreateTestItemAsync(sku: "ELEC-002", category: "Electronics");
        await CreateTestItemAsync(sku: "BOOK-001", category: "Books");

        // Act
        var response = await _client.GetAsync("/api/items?category=Electronics");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<PagedItemsResult>(_jsonOptions);
        result!.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(i => i.Category == "Electronics");
    }

    [Fact]
    public async Task GetItems_WithSearchTerm_ShouldReturnMatchingItems()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        await CreateTestItemAsync(sku: "LAPTOP-001", name: "Gaming Laptop");
        await CreateTestItemAsync(sku: "MOUSE-001", name: "Gaming Mouse");
        await CreateTestItemAsync(sku: "DESK-001", name: "Office Desk");

        // Act
        var response = await _client.GetAsync("/api/items?search=Gaming");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<PagedItemsResult>(_jsonOptions);
        result!.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(i => i.Name.Contains("Gaming"));
    }

    #endregion

    #region PUT /api/items/{id} Tests

    [Fact]
    public async Task UpdateItem_WithValidData_ShouldReturnUpdatedItem()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var item = await CreateTestItemAsync();
        var updateDto = new UpdateItemDto(
            Name: "Updated Product",
            Description: "Updated Description",
            Price: 149.99m,
            Category: "Updated Category"
        );

        // Act
        var response = await _client.PutAsJsonAsync($"/api/items/{item.Id}", updateDto, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<ItemDto>(_jsonOptions);
        result!.Name.Should().Be(updateDto.Name);
        result.Price.Should().Be(updateDto.Price);
        result.SKU.Should().Be(item.SKU); // SKU should not change
    }

    #endregion

    #region Stock Management Tests

    [Fact]
    public async Task AdjustStock_WithValidQuantity_ShouldUpdateStock()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var item = await CreateTestItemAsync(initialStock: 50);
        var adjustmentDto = new StockAdjustmentDto(
            NewQuantity: 75,
            Reason: "Inventory recount"
        );

        // Act
        var response = await _client.PutAsJsonAsync($"/api/items/{item.Id}/stock", adjustmentDto, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<ItemDto>(_jsonOptions);
        result!.AvailableStock.Should().Be(75);
    }

    [Fact]
    public async Task ReserveStock_WithAvailableQuantity_ShouldReserveStock()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var item = await CreateTestItemAsync(initialStock: 100);
        var reservationDto = new StockReservationDto(
            Quantity: 20,
            OrderId: Guid.NewGuid().ToString()
        );

        // Act
        var response = await _client.PostAsJsonAsync($"/api/items/{item.Id}/reserve-stock", reservationDto, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<ItemDto>(_jsonOptions);
        result!.AvailableStock.Should().Be(80);
        result.ReservedStock.Should().Be(20);
    }

    [Fact]
    public async Task ReserveStock_WithInsufficientQuantity_ShouldReturnConflict()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var item = await CreateTestItemAsync(initialStock: 10);
        var reservationDto = new StockReservationDto(
            Quantity: 20,
            OrderId: Guid.NewGuid().ToString()
        );

        // Act
        var response = await _client.PostAsJsonAsync($"/api/items/{item.Id}/reserve-stock", reservationDto, _jsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        problemDetails!.Detail.Should().Contain("Cannot reserve");
    }

    #endregion

    #region Concurrency Tests

    [Fact]
    public async Task ConcurrentStockUpdates_ShouldHandleOptimisticConcurrency()
    {
        // Arrange
        await _factory.ClearDatabaseAsync();
        var item = await CreateTestItemAsync(initialStock: 100);

        // Create two concurrent update tasks
        var task1 = _client.PutAsJsonAsync($"/api/items/{item.Id}/stock", 
            new StockAdjustmentDto(50, "Update 1"), _jsonOptions);
        
        var task2 = _client.PutAsJsonAsync($"/api/items/{item.Id}/stock", 
            new StockAdjustmentDto(75, "Update 2"), _jsonOptions);

        // Act
        var responses = await Task.WhenAll(task1, task2);

        // Assert
        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        var conflictCount = responses.Count(r => r.StatusCode == HttpStatusCode.Conflict);
        
        // One should succeed, one should get conflict
        successCount.Should().Be(1);
        conflictCount.Should().Be(1);
    }

    #endregion

    #region Helper Methods

    private async Task<ItemDto> CreateTestItemAsync(
        string sku = "TEST-001",
        string name = "Test Product",
        decimal price = 99.99m,
        int initialStock = 100,
        string category = "Test Category")
    {
        var createDto = new CreateItemDto(sku, name, "Test Description", price, initialStock, category);
        
        var response = await _client.PostAsJsonAsync("/api/items", createDto, _jsonOptions);
        response.EnsureSuccessStatusCode();
        
        return await response.Content.ReadFromJsonAsync<ItemDto>(_jsonOptions) 
               ?? throw new InvalidOperationException("Failed to create test item");
    }

    private async Task CreateMultipleTestItemsAsync(int count)
    {
        var tasks = new List<Task>();
        for (int i = 0; i < count; i++)
        {
            tasks.Add(CreateTestItemAsync(
                sku: $"TEST-{i:D3}",
                name: $"Test Product {i}",
                price: 10m + i,
                category: i % 2 == 0 ? "Electronics" : "Books"
            ));
        }
        await Task.WhenAll(tasks);
    }

    #endregion

    public async ValueTask DisposeAsync()
    {
        await _factory.ClearDatabaseAsync();
        _client.Dispose();
    }
}
```

### Phase 7: Performance Optimization and Monitoring
**Objective**: Ensure performance requirements are met

#### 7.1 Add Performance Metrics

```csharp
// File: src/WorkerService.Application/Common/Metrics/ItemApiMetrics.cs
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace WorkerService.Application.Common.Metrics;

public class ItemApiMetrics
{
    private readonly Counter<long> _itemsCreated;
    private readonly Counter<long> _stockAdjustments;
    private readonly Counter<long> _stockReservations;
    private readonly Histogram<double> _queryDuration;
    private readonly UpDownCounter<long> _activeItems;

    public ItemApiMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("ItemsAPI");

        _itemsCreated = meter.CreateCounter<long>("items.created", description: "Total number of items created");
        _stockAdjustments = meter.CreateCounter<long>("items.stock.adjustments", description: "Total stock adjustments");
        _stockReservations = meter.CreateCounter<long>("items.stock.reservations", description: "Total stock reservations");
        _queryDuration = meter.CreateHistogram<double>("items.query.duration", "ms", "Duration of item queries");
        _activeItems = meter.CreateUpDownCounter<long>("items.active", description: "Number of active items");
    }

    public void RecordItemCreated() => _itemsCreated.Add(1);
    public void RecordStockAdjustment() => _stockAdjustments.Add(1);
    public void RecordStockReservation() => _stockReservations.Add(1);
    public void RecordQueryDuration(double milliseconds) => _queryDuration.Record(milliseconds);
    public void IncrementActiveItems() => _activeItems.Add(1);
    public void DecrementActiveItems() => _activeItems.Add(-1);
}
```

## 🚨 Technical Considerations

### Optimistic Concurrency Control
- Version field on Item entity for concurrent stock updates
- Retry logic in handlers for concurrency conflicts
- Clear error messages for users when conflicts occur

### Performance Optimizations
- Indexed columns: SKU (unique), Category, IsActive
- Composite index on (Category, IsActive) for filtered queries
- Efficient pagination with database-level filtering
- Projection queries to reduce data transfer

### Data Migration Strategy
1. Deploy Items feature with backward compatibility
2. Run data migration script to create Items from OrderItem.ProductId
3. Update Order creation to use Item references
4. Monitor for orphaned ProductIds
5. Eventually deprecate ProductId field

### Security Considerations
- Input validation at API and domain levels
- SKU format enforcement to prevent injection
- Rate limiting on stock adjustment endpoints
- Audit logging for all stock changes

## 🔍 Validation Gates

### Gate 1: Domain Layer Tests
```bash
cd tests/WorkerService.UnitTests
dotnet test --filter "FullyQualifiedName~Domain.Item"
```
**Expected**: All Item entity, value object, and domain event tests pass

### Gate 2: Application Layer Tests
```bash
cd tests/WorkerService.UnitTests
dotnet test --filter "FullyQualifiedName~Handlers.*Item"
```
**Expected**: All CQRS handler and validator tests pass

### Gate 3: Database Migration
```bash
cd src/WorkerService.Worker
dotnet ef database update
```
**Expected**: Items table created with proper constraints and indexes

### Gate 4: API Integration Tests
```bash
cd tests/WorkerService.IntegrationTests
dotnet test --filter "FullyQualifiedName~ItemsApi"
```
**Expected**: All endpoint tests pass with >90% coverage

### Gate 5: Performance Test
```bash
# Create 1000 test items via API
# Then run:
curl -w "@curl-format.txt" -o /dev/null -s "http://localhost:5000/api/items?pageSize=100"
```
**Expected**: Response time <100ms for 1000 items

### Gate 6: Concurrency Test
```bash
# Run concurrent stock updates
parallel -j 10 curl -X PUT http://localhost:5000/api/items/{id}/stock \
  -H "Content-Type: application/json" \
  -d '{"newQuantity": 50, "reason": "Test"}' ::: {1..10}
```
**Expected**: Proper handling of concurrent updates with appropriate conflict responses

### Gate 7: End-to-End Order Integration
```bash
# Create item, then create order referencing the item
# Verify order creation succeeds with item validation
```
**Expected**: Orders can successfully reference items

## 📈 Rollout Strategy

1. **Phase 1**: Deploy Items API (Week 1)
   - Items management fully functional
   - No impact on existing Orders

2. **Phase 2**: Data Migration (Week 2)
   - Create Items from existing ProductIds
   - Add ItemId to OrderItems

3. **Phase 3**: Update Order Creation (Week 3)
   - Orders validate against Items
   - Dual-write to ProductId and ItemId

4. **Phase 4**: Deprecation (Week 4+)
   - Mark ProductId as obsolete
   - Monitor usage and plan removal

## 🎯 Definition of Done

- [ ] All unit tests passing with >95% coverage
- [ ] All integration tests passing
- [ ] Performance benchmarks met (<100ms for 1000 items)
- [ ] API documentation complete in OpenAPI/Swagger
- [ ] Database migrations tested in staging
- [ ] Concurrent update handling verified
- [ ] Backward compatibility maintained
- [ ] Monitoring and metrics implemented
- [ ] Security review completed
- [ ] Code review approved
- [ ] Deployed to staging environment
- [ ] Load testing completed successfully

## 🚀 Next Steps

1. Review and approve this PRP
2. Proceed on the same branch, no need to create a feature branch
3. Proceed through phases with validation gates

This PRP provides a complete, production-ready implementation plan for the Item CRUD API with minimal endpoints, following all architectural guidelines and best practices.