# Worker Service Feature Request

## FEATURE NAME:

Item CRUD API with Minimal API Endpoints

---

## FEATURE PURPOSE:

To provide a comprehensive Item management system that operates independently from Orders, enabling external systems and client applications to manage product catalog items through RESTful HTTP minimal API endpoints. This feature establishes Items as first-class entities in the domain model, decoupling product information from order line items, and providing a centralized product catalog management capability. The implementation strictly follows minimal API patterns as mandated by the codebase standards, avoiding controller-based approaches.

---

## CORE FUNCTIONALITY:

- **Item Entity Management**: Create a new Item aggregate root entity separate from OrderItem, with properties like Name, Description, SKU, Price, StockQuantity, Category, and IsActive status.
- **Item Creation Endpoint**: POST `/api/items` to create new items with comprehensive validation for SKU uniqueness, price validity, and required fields.
- **Item Retrieval Endpoints**: 
  - GET `/api/items/{id}` to retrieve a specific item by ID
  - GET `/api/items` to retrieve a paginated list of items with filtering by category, active status, and search by name/SKU
  - GET `/api/items/sku/{sku}` to retrieve an item by its unique SKU
- **Item Update Endpoint**: PUT `/api/items/{id}` to update existing items with optimistic concurrency control and validation.
- **Item Deletion Endpoint**: DELETE `/api/items/{id}` to soft-delete items (mark as inactive) preserving referential integrity with existing orders.
- **Stock Management Endpoints**:
  - PUT `/api/items/{id}/stock` to adjust stock quantity
  - POST `/api/items/{id}/reserve-stock` to reserve stock for pending orders
- **Minimal API Implementation**: All endpoints implemented using ASP.NET Core minimal APIs with proper request/response mapping, validation, and error handling.
- **Integration with Orders**: Update OrderItem to reference Item entity by ID instead of storing ProductId string, enabling proper foreign key relationships.

---

## CLEAN ARCHITECTURE LAYERS:

This feature follows Clean Architecture principles with clear separation of concerns:

### Domain Layer (`WorkerService.Domain`)
- **Item Entity**: New aggregate root with business rules for pricing, stock management, and SKU uniqueness
- **Value Objects**: `SKU`, `Price`, `StockLevel` value objects with validation
- **Domain Events**: `ItemCreatedEvent`, `ItemUpdatedEvent`, `ItemDeactivatedEvent`, `StockAdjustedEvent`
- **Repository Interface**: `IItemRepository` with methods for CRUD operations and SKU lookup
- **Domain Services**: `IStockManagementService` for complex stock operations

### Application Layer (`WorkerService.Application`)
- **CQRS Commands**: `CreateItemCommand`, `UpdateItemCommand`, `DeactivateItemCommand`, `AdjustStockCommand`
- **CQRS Queries**: `GetItemQuery`, `GetItemsQuery`, `GetItemBySkuQuery`
- **Command/Query Handlers**: Implementation of all CQRS handlers with business logic
- **DTOs**: `ItemDto`, `CreateItemDto`, `UpdateItemDto`, `ItemListDto`, `StockAdjustmentDto`
- **Validators**: FluentValidation validators for all commands ensuring business rule compliance
- **Mapping**: Manual mapping between entities and DTOs (no AutoMapper)
- **Application Services**: `IItemService` for orchestrating complex operations

### Infrastructure Layer (`WorkerService.Infrastructure`)
- **Repository Implementation**: `ItemRepository` with EF Core implementation
- **Database Configuration**: Entity type configuration for Item with indexes on SKU
- **Database Migrations**: Create Items table with proper constraints and indexes
- **Message Publishing**: Publish domain events through MassTransit for downstream consumers

### Worker Layer (`WorkerService.Worker`)
- **Minimal API Endpoints**: All HTTP endpoints defined using minimal API pattern in `Program.cs`
- **Request/Response Models**: Endpoint-specific models for API contracts
- **Validation Middleware**: Request validation using FluentValidation
- **Error Handling**: Global exception handling for consistent error responses
- **OpenAPI Documentation**: Swagger documentation for all endpoints

---

## CONFIGURATION REQUIREMENTS:

### 1. Minimal API Configuration in Program.cs

```csharp
// Item API endpoints using minimal APIs
app.MapGroup("/api/items")
    .WithTags("Items")
    .WithOpenApi()
    .MapItemEndpoints();

// Extension method for organizing endpoints
public static class ItemEndpoints
{
    public static RouteGroupBuilder MapItemEndpoints(this RouteGroupBuilder group)
    {
        // POST /api/items
        group.MapPost("/", async (
            CreateItemDto dto,
            IValidator<CreateItemCommand> validator,
            IMediator mediator,
            CancellationToken ct) =>
        {
            // Implementation
        })
        .WithName("CreateItem")
        .Produces<ItemDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem();

        // GET /api/items/{id}
        group.MapGet("/{id:guid}", async (
            Guid id,
            IMediator mediator,
            CancellationToken ct) =>
        {
            // Implementation
        })
        .WithName("GetItem")
        .Produces<ItemDto>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        // Additional endpoints...
        
        return group;
    }
}
```

### 2. Database Configuration

**Entity Configuration:**
```csharp
public class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.HasKey(i => i.Id);
        builder.HasIndex(i => i.SKU).IsUnique();
        builder.Property(i => i.Name).IsRequired().HasMaxLength(200);
        builder.Property(i => i.SKU).IsRequired().HasMaxLength(50);
        builder.OwnsOne(i => i.Price);
        builder.OwnsOne(i => i.StockLevel);
    }
}
```

### 3. Application Settings

**`appsettings.json`:**
```json
{
  "ItemManagement": {
    "EnableStockTracking": true,
    "DefaultPageSize": 20,
    "MaxPageSize": 100,
    "AllowNegativeStock": false,
    "RequireUniqueSkus": true
  }
}
```
Make sure to update all `appsettings.*.json` files

### 4. Impact on Orders API

**Migration Strategy for OrderItem:**
- Add `ItemId` property to OrderItem entity
- Create data migration to populate ItemId based on existing ProductId
- Update Order aggregate to validate Items exist when creating OrderItems
- Deprecate ProductId property (maintain for backward compatibility initially)

---

## PACKAGE MANAGEMENT:

### Required NuGet Packages

No additional packages required - the implementation will use existing packages:
- Entity Framework Core (already present)
- FluentValidation (already present)
- MediatR (already present)
- MassTransit (already present)

### Package Removals

Since we're moving from Controllers to Minimal APIs:
- Consider removing controller-specific packages if no other controllers remain

---

## TESTING REQUIREMENTS:

### Unit Testing
- **Domain Tests**: Item entity business rules, value object validation, domain service logic
- **Handler Tests**: All CQRS command and query handlers with mocked dependencies
- **Validator Tests**: FluentValidation rules for all commands
- **Mapping Tests**: Verify correct mapping between entities and DTOs

### Integration Testing
- **API Endpoint Tests**: Full HTTP request/response testing for all minimal API endpoints
- **Database Tests**: Repository operations, unique constraint validation, concurrent updates
- **Stock Management Tests**: Complex scenarios involving stock reservation and adjustment
- **Order Integration Tests**: Verify Orders can correctly reference Items

### Test Scenarios
1. **Item Creation**: Valid/invalid data, duplicate SKUs, price validation
2. **Item Retrieval**: By ID, by SKU, pagination, filtering, sorting
3. **Item Updates**: Optimistic concurrency, partial updates, validation
4. **Stock Operations**: Adjustments, reservations, negative stock prevention
5. **Soft Deletion**: Deactivation, impact on existing orders
6. **Performance**: Pagination efficiency, index usage

### Manual Testing Verification
1. **Swagger UI**: All endpoints documented and testable via Swagger
2. **CRUD Operations**: Complete lifecycle testing via HTTP client
3. **Stock Management**: Real-time stock updates and reservations
4. **Order Integration**: Create orders referencing items
5. **Concurrent Access**: Multiple clients updating same items

---

## SUCCESS CRITERIA:

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

---

## EXPECTED COMPLEXITY LEVEL:

- [ ] **Low** - Involves straightforward configuration changes and conditional DI registration.
- [X] **Intermediate** - Production-ready patterns with multiple integrated technologies
- [ ] **Advanced** - Complex scenarios with advanced patterns

**Reasoning:** This feature requires implementing a complete bounded context with its own aggregate root, establishing proper relationships with the existing Order aggregate, migrating from controller-based to minimal API patterns, and ensuring backward compatibility. The complexity lies in properly decoupling Items from Orders while maintaining referential integrity, implementing stock management with concurrency control, and refactoring the existing API pattern from controllers to minimal APIs. This represents a significant architectural change that impacts multiple layers of the application while requiring careful consideration of data migration and backward compatibility.

---

## IMPLEMENTATION NOTES:

### Critical Considerations:

1. **Minimal API Pattern**: Must strictly follow minimal API patterns - no controllers allowed per CLAUDE.md rules
2. **Data Migration**: Existing orders reference products by ProductId string - need migration strategy
3. **Referential Integrity**: Soft-deleted items must remain accessible for historical orders
4. **Stock Management**: Must handle concurrent stock operations safely
5. **Performance**: SKU lookups must be indexed for efficient retrieval
6. **Backward Compatibility**: Initial implementation should maintain ProductId on OrderItem during transition period

### Recommended Implementation Order:

1. Create Item domain entity and value objects
2. Implement repository and database migrations
3. Create CQRS handlers and validators
4. Implement minimal API endpoints
5. Update OrderItem to support ItemId
6. Create data migration for existing orders
7. Implement stock management features
8. Add comprehensive testing
9. Update documentation

### Risk Mitigation:

- Maintain ProductId field temporarily for rollback capability
