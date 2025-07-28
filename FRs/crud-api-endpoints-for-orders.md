# Worker Service Feature Request

## FEATURE NAME:

CRUD API Endpoints for Orders

---

## FEATURE PURPOSE:

To provide RESTful HTTP API endpoints for Create, Read, Update, and Delete operations on Order entities, enabling external systems and client applications to manage orders through standardized HTTP requests. This feature transforms the worker service into a hybrid application that processes background tasks while also serving API requests, providing a unified interface for order management operations with proper validation, error handling, and observability.

---

## CORE FUNCTIONALITY:

- **Order Creation Endpoint**: POST `/api/orders` to create new orders with automatic validation, business rule enforcement, and message publishing for downstream processing.
- **Order Retrieval Endpoints**: 
  - GET `/api/orders/{id}` to retrieve a specific order by ID
  - GET `/api/orders` to retrieve a paginated list of orders with filtering and sorting capabilities
- **Order Update Endpoint**: PUT `/api/orders/{id}` to update existing orders with optimistic concurrency control and validation.
- **Order Deletion Endpoint**: DELETE `/api/orders/{id}` to soft-delete orders with audit trail preservation.
- **RESTful Response Standards**: Consistent HTTP status codes, error responses, and resource representations following REST conventions.
- **Request/Response Validation**: Input validation using FluentValidation with detailed error messages and model binding.
- **API Documentation**: OpenAPI/Swagger documentation for all endpoints with examples and schema definitions.
- **Content Negotiation**: Support for JSON request/response format with proper content-type handling.

---

## CLEAN ARCHITECTURE LAYERS:

This feature impacts multiple layers while maintaining strict Clean Architecture boundaries:

### Domain Layer (`WorkerService.Domain`)
- Order entity definition with business rules and validation
- Value objects for order components (OrderItem, Address, etc.)
- Domain events for order state changes
- Repository interface definitions

### Application Layer (`WorkerService.Application`)
- CQRS command handlers for Create, Update, Delete operations
- CQRS query handlers for Read operations with DTOs
- FluentValidation validators for all command inputs
- Application service interfaces for order operations
- Request/Response DTOs with proper mapping

### Infrastructure Layer (`WorkerService.Infrastructure`)
- Repository implementations with EF Core
- Database migrations for Order-related tables
- MassTransit message publishing for order events
- AutoMapper profiles for entity-to-DTO mapping

### Worker Layer (`WorkerService.Worker`)
- API controller with proper routing and HTTP verb mapping
- Minimal API configuration and middleware setup
- Dependency injection registration for API components
- OpenAPI/Swagger configuration

--- 

## CONFIGURATION REQUIREMENTS:

### 1. Web API Configuration in Program.cs

The worker service needs to be configured to support both background services and web API capabilities.

**Changes to `Program.cs`:**
```csharp
// Change SDK from Microsoft.NET.Sdk to Microsoft.NET.Sdk.Web in .csproj
// Add web API services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure routing and middleware pipeline
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.MapControllers();
```

### 2. API Configuration Settings

**`appsettings.json`:**
```json
{
  "ApiSettings": {
    "DefaultPageSize": 20,
    "MaxPageSize": 100,
    "EnableSwagger": true,
    "CorsOrigins": ["http://localhost:3000", "https://localhost:3001"]
  }
}
```

**Environment Variables:**
- `ApiSettings__DefaultPageSize`
- `ApiSettings__MaxPageSize`
- `ApiSettings__EnableSwagger`
- `ASPNETCORE_URLS=http://localhost:5000;https://localhost:5001`

### 3. Database Configuration

Entity Framework migrations and DbSet configuration for Orders:

```csharp
// In ApplicationDbContext
public DbSet<Order> Orders { get; set; }
public DbSet<OrderItem> OrderItems { get; set; }
```

--- 

## PACKAGE MANAGEMENT:

### Required NuGet Packages

**`WorkerService.Worker` project**:
- `Microsoft.AspNetCore.OpenApi` (for OpenAPI support)
- `Swashbuckle.AspNetCore` (for Swagger UI)

**`WorkerService.Application` project**:
- `AutoMapper` (for entity-to-DTO mapping)
- `AutoMapper.Extensions.Microsoft.DependencyInjection`

**`WorkerService.Infrastructure` project**:
- No additional packages required (existing EF Core packages sufficient)

### Project File Changes

**`WorkerService.Worker.csproj`** must change SDK:
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
```

--- 

## TESTING REQUIREMENTS:

### Unit Testing
- **Controller Tests**: Verify proper HTTP status codes, response formats, and error handling
- **Command/Query Handler Tests**: Test business logic in isolation with mocked dependencies
- **Validation Tests**: Verify FluentValidation rules with valid and invalid inputs
- **Mapping Tests**: Ensure AutoMapper profiles correctly map between entities and DTOs

### Integration Testing
- **API Endpoint Tests**: Full HTTP request/response testing with test database
- **Database Integration Tests**: Verify EF Core operations work correctly with API calls
- **Message Publishing Tests**: Confirm MassTransit messages are published on order operations

### Manual Testing Verification
1. **API Documentation**: Swagger UI accessible at `/swagger` with complete endpoint documentation
2. **CRUD Operations**: Successful Create, Read, Update, Delete operations via HTTP requests
3. **Validation**: Proper error responses for invalid input data
4. **Pagination**: GET `/api/orders` returns paginated results with metadata
5. **Observability**: API calls generate proper OpenTelemetry traces and metrics

---

## SUCCESS CRITERIA:

- [ ] Order entity and related value objects defined in Domain layer with business rules
- [ ] Complete CQRS command/query handlers implemented in Application layer
- [ ] FluentValidation validators created for all API input models
- [ ] OrdersController implemented with all CRUD endpoints (GET, POST, PUT, DELETE)
- [ ] Database migrations created for Order and OrderItem tables
- [ ] AutoMapper profiles configured for entity-to-DTO mapping
- [ ] OpenAPI/Swagger documentation accessible and complete
- [ ] All API endpoints return proper HTTP status codes (200, 201, 400, 404, 500)
- [ ] Pagination implemented for GET `/api/orders` with configurable page size
- [ ] Input validation provides detailed error messages for invalid requests
- [ ] MassTransit messages published for order creation and updates
- [ ] API endpoints generate OpenTelemetry traces and metrics
- [ ] Health checks include API endpoint availability
- [ ] Integration tests cover all CRUD operations with >90% code coverage
- [ ] API follows RESTful conventions and proper resource naming

---

## EXPECTED COMPLEXITY LEVEL:

- [ ] **Low** - Involves straightforward configuration changes and conditional DI registration.
- [X] **Intermediate** - Production-ready patterns with multiple integrated technologies
- [ ] **Advanced** - Complex scenarios with advanced patterns

**Reasoning:** This feature requires implementing a complete API layer with proper Clean Architecture boundaries, CQRS patterns, validation, mapping, database operations, message publishing, and observability. While each individual component is well-understood, integrating them all correctly while maintaining clean separation of concerns and ensuring production-ready quality (error handling, validation, documentation, testing) represents intermediate complexity. The transformation from a pure worker service to a hybrid API+worker application requires careful consideration of the hosting model and dependency injection configuration.