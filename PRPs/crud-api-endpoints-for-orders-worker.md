# Project Requirement Plan (PRP): CRUD API Endpoints for Orders in .NET 9 Worker Service

## Executive Summary

This PRP outlines the implementation of RESTful CRUD API endpoints for Order management in a .NET 9 Worker Service application following Clean Architecture principles. The implementation transforms the existing background-processing Worker Service into a hybrid application that maintains all background processing capabilities while adding comprehensive Web API functionality.

Based on extensive research of .NET 9 patterns, this implementation leverages:
- **.NET 9 Native OpenAPI support** replacing traditional Swagger dependencies
- **Hybrid Worker+API architecture** using Microsoft.NET.Sdk.Web
- **Clean Architecture with CQRS/MediatR** for separation of concerns
- **EF Core 9 with PostgreSQL** and proper DbContext lifetime management
- **MassTransit integration** for message publishing from API endpoints
- **OpenTelemetry instrumentation** for comprehensive observability
- **FluentValidation** for robust input validation
- **AutoMapper** for efficient entity-DTO mapping

**Confidence Level**: 9/10 - Implementation plan based on latest .NET 9 patterns with production-ready considerations.

---

## Current State Analysis

### Existing Architecture Assessment

**Strengths Identified:**
- ✅ Clean Architecture layers properly implemented (Domain, Application, Infrastructure, Worker)
- ✅ CQRS pattern with MediatR already configured
- ✅ Domain entities (Order, OrderItem) with business rules and domain events
- ✅ FluentValidation infrastructure present
- ✅ MassTransit configured for message publishing
- ✅ OpenTelemetry comprehensive instrumentation setup
- ✅ Project already uses Microsoft.NET.Sdk.Web (ready for API endpoints)
- ✅ Repository pattern with IOrderRepository interface
- ✅ Background services (OrderProcessingService, MetricsCollectionService)

**Current CQRS Implementation:**
- CreateOrderCommand/CreateOrderCommandHandler exists
- CreateOrderCommandValidator implemented
- GetOrderQuery/GetOrderQueryHandler exists
- Repository abstractions properly defined

**Infrastructure Ready:**
- ApplicationDbContext with Order/OrderItem DbSets
- PostgreSQL + In-Memory provider conditional configuration
- MassTransit with OrderCreatedConsumer
- Health checks configured
- Comprehensive OpenTelemetry setup

### Architecture Compliance Verification

**Domain Layer**: ✅ No external dependencies, pure POCO entities with business logic
**Application Layer**: ✅ Only depends on Domain, contains CQRS handlers and validation
**Infrastructure Layer**: ✅ Depends on Application and Domain, implements data access and messaging
**Worker Layer**: ✅ Depends on Infrastructure and Application, hosts background services

---

## Technology Foundation Research Findings

### .NET 9 Hybrid Worker Service + Web API Architecture

Research confirms that .NET 9 supports robust hybrid applications combining BackgroundService with Web API:

**Key Patterns:**
- Use `Microsoft.NET.Sdk.Web` for combined hosting capabilities
- BackgroundService runs alongside ASP.NET Core pipeline
- Shared DI container and configuration between API and background services
- Common telemetry and health check endpoints

**Latest Configuration Pattern (2025):**
```csharp
var builder = WebApplication.CreateBuilder(args);

// Add both API and background service capabilities
builder.Services.AddControllers();
builder.Services.AddHostedService<OrderProcessingService>();
builder.Services.AddOpenApi(); // .NET 9 native OpenAPI

var app = builder.Build();
app.MapControllers();
app.MapOpenApi();
await app.RunAsync();
```

### .NET 9 Native OpenAPI Support

**Major Change in .NET 9:**
- Microsoft removed dependency on Swashbuckle/NSwag
- New `Microsoft.AspNetCore.OpenApi` package provides native support
- Source Generators for OpenAPI document creation
- Native AoT compatibility
- Better performance and reduced dependencies

**Migration Strategy:**
- Use `Microsoft.AspNetCore.OpenApi` for document generation
- Add `Swashbuckle.AspNetCore` optionally for UI (backward compatibility)
- Leverage `MapOpenApi()` for endpoint registration

### CQRS with MediatR in .NET 9

Research shows continued strong support for MediatR patterns:

**Best Practices:**
- Command handlers in Application layer return Results for better error handling
- Query handlers return DTOs directly (not domain entities)
- Use `IRequestHandler<TRequest, TResponse>` consistently
- Leverage pipeline behaviors for cross-cutting concerns

### EF Core 9 with PostgreSQL - DbContext Lifetime Management

**Critical Pattern for Worker Services:**
- Use `IServiceScopeFactory` in background services
- DbContext pooling for high-performance scenarios
- Npgsql 9.0.4+ for .NET 9 compatibility

**Recommended Configuration:**
```csharp
builder.Services.AddDbContextPool<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
```

### MassTransit Integration with Web API

**Integration Patterns:**
- Publish messages from CQRS command handlers
- Use `IPublishEndpoint` injection in handlers
- Maintain correlation IDs from API requests through messages
- Configure OpenTelemetry for message tracing

### FluentValidation Manual Validation Pattern

**Recommended Approach (deprecated auto-validation):**
- Manual validation in controllers using `IValidator<T>`
- Works with Minimal APIs
- Better control over validation flow
- Async validation support

---

## Implementation Strategy

### Phase 1: Project Configuration and Dependencies

**1.1 NuGet Package Updates**
```xml
<!-- Add to WorkerService.Worker.csproj -->
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="9.0.0" />
<PackageReference Include="Swashbuckle.AspNetCore" Version="7.2.0" />

<!-- Add to WorkerService.Application.csproj -->
<PackageReference Include="AutoMapper" Version="13.0.1" />
<PackageReference Include="AutoMapper.Extensions.Microsoft.DependencyInjection" Version="13.0.1" />
```

**1.2 Project Configuration Verification**
- ✅ WorkerService.Worker.csproj already uses `Microsoft.NET.Sdk.Web`
- ✅ All required OpenTelemetry packages present
- ✅ MediatR and FluentValidation configured

### Phase 2: Domain Layer Enhancements

**2.1 Domain Events Enhancement** (Already implemented)
- ✅ OrderCreatedEvent, OrderValidatedEvent, OrderPaidEvent, etc.
- ✅ IDomainEvent interface
- ✅ Domain event raising in Order entity

**2.2 Value Objects** (Already implemented)
- ✅ Money value object
- ✅ OrderItem entity with business rules

**2.3 Repository Interface Enhancement**
```csharp
// Add pagination support to IOrderRepository
Task<(IEnumerable<Order> Orders, int TotalCount)> GetPagedAsync(
    int pageNumber, int pageSize, CancellationToken cancellationToken = default);
```

### Phase 3: Application Layer Implementation

**3.1 CQRS Commands Extension**
```csharp
// UpdateOrderCommand
public record UpdateOrderCommand(
    Guid OrderId,
    string CustomerId,
    IList<OrderItemDto> Items) : IRequest<UpdateOrderResult>;

// DeleteOrderCommand  
public record DeleteOrderCommand(Guid OrderId) : IRequest<bool>;

// GetOrdersQuery (with pagination)
public record GetOrdersQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? CustomerId = null) : IRequest<PagedOrdersResult>;
```

**3.2 Response DTOs**
```csharp
public record OrderResponseDto(
    Guid Id,
    string CustomerId,
    DateTime OrderDate,
    string Status,
    decimal TotalAmount,
    IEnumerable<OrderItemResponseDto> Items);

public record PagedOrdersResult(
    IEnumerable<OrderResponseDto> Orders,
    int TotalCount,
    int PageNumber,
    int PageSize,
    bool HasNextPage,
    bool HasPreviousPage);
```

**3.3 Validation Enhancement**
```csharp
public class UpdateOrderCommandValidator : AbstractValidator<UpdateOrderCommand>
{
    public UpdateOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.CustomerId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).SetValidator(new OrderItemDtoValidator());
    }
}
```

### Phase 4: Infrastructure Layer Implementation

**4.1 Repository Enhancement**
```csharp
public async Task<(IEnumerable<Order> Orders, int TotalCount)> GetPagedAsync(
    int pageNumber, int pageSize, CancellationToken cancellationToken = default)
{
    var query = _context.Orders.Include(o => o.Items);
    
    var totalCount = await query.CountAsync(cancellationToken);
    var orders = await query
        .OrderByDescending(o => o.CreatedAt)
        .Skip((pageNumber - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(cancellationToken);
    
    return (orders, totalCount);
}
```

**4.2 AutoMapper Configuration**
```csharp
public class OrderMappingProfile : Profile
{
    public OrderMappingProfile()
    {
        CreateMap<Order, OrderResponseDto>()
            .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()));
        
        CreateMap<OrderItem, OrderItemResponseDto>();
        
        CreateMap<CreateOrderCommand, Order>()
            .ConstructUsing((src, context) => 
                new Order(src.CustomerId, 
                    src.Items.Select(i => new OrderItem(i.ProductId, i.Quantity, new Money(i.UnitPrice)))));
    }
}
```

### Phase 5: Worker Layer API Implementation

**5.1 OrdersController Implementation**
```csharp
[ApiController]
[Route("api/[controller]")]
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
    [HttpPost]
    [ProducesResponseType<CreateOrderResult>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        // Manual validation (recommended pattern)
        var validationResult = await _createValidator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        var result = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetOrder), new { id = result.OrderId }, result);
    }

    /// <summary>
    /// Gets an order by ID
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<OrderResponseDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOrder(Guid id, CancellationToken cancellationToken)
    {
        var query = new GetOrderQuery(id);
        var result = await _mediator.Send(query, cancellationToken);
        
        return result != null ? Ok(result) : NotFound();
    }

    /// <summary>
    /// Gets paginated list of orders
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedOrdersResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrders(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? customerId = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetOrdersQuery(pageNumber, pageSize, customerId);
        var result = await _mediator.Send(query, cancellationToken);
        
        return Ok(result);
    }

    /// <summary>
    /// Updates an existing order
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<UpdateOrderResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateOrder(
        Guid id,
        [FromBody] UpdateOrderCommand command,
        CancellationToken cancellationToken)
    {
        if (id != command.OrderId)
        {
            return BadRequest("Order ID mismatch");
        }

        var validationResult = await _updateValidator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return BadRequest(validationResult.Errors);
        }

        var result = await _mediator.Send(command, cancellationToken);
        return result != null ? Ok(result) : NotFound();
    }

    /// <summary>
    /// Soft deletes an order
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteOrder(Guid id, CancellationToken cancellationToken)
    {
        var command = new DeleteOrderCommand(id);
        var result = await _mediator.Send(command, cancellationToken);
        
        return result ? NoContent() : NotFound();
    }
}
```

**5.2 Program.cs Enhancement**
```csharp
// Add after existing configurations (around line 147)

// Configure Web API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure .NET 9 Native OpenAPI
builder.Services.AddOpenApi();

// Configure AutoMapper
builder.Services.AddAutoMapper(typeof(OrderMappingProfile));

// Configure CORS for development
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000", "https://localhost:3001")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// After app.MapHealthChecks() configurations (around line 174)

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors();
}

// Map API endpoints
app.MapControllers();
app.MapOpenApi(); // .NET 9 native OpenAPI endpoint
```

---

## Database Design

### Entity Configuration Enhancement

**ApplicationDbContext Update:**
```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // Order configuration
    modelBuilder.Entity<Order>(entity =>
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.CustomerId).IsRequired().HasMaxLength(100);
        entity.Property(e => e.OrderDate).IsRequired();
        entity.Property(e => e.Status).HasConversion<string>().IsRequired();
        entity.Property(e => e.CreatedAt).IsRequired();
        entity.Property(e => e.UpdatedAt).IsRequired();
        
        // Money value object configuration
        entity.OwnsOne(e => e.TotalAmount, money =>
        {
            money.Property(m => m.Amount).HasColumnName("TotalAmount").HasPrecision(18, 2);
        });

        // Configure collection
        entity.HasMany(e => e.Items)
              .WithOne()
              .HasForeignKey("OrderId")
              .OnDelete(DeleteBehavior.Cascade);

        // Indexes for performance
        entity.HasIndex(e => e.CustomerId);
        entity.HasIndex(e => e.OrderDate);
        entity.HasIndex(e => e.Status);
    });

    // OrderItem configuration
    modelBuilder.Entity<OrderItem>(entity =>
    {
        entity.HasKey(e => e.Id);
        entity.Property(e => e.ProductId).IsRequired().HasMaxLength(100);
        entity.Property(e => e.Quantity).IsRequired();
        
        entity.OwnsOne(e => e.UnitPrice, money =>
        {
            money.Property(m => m.Amount).HasColumnName("UnitPrice").HasPrecision(18, 2);
        });
    });
}
```

---

## API Design Specification

### RESTful Endpoint Specifications

| HTTP Verb | Route | Description | Request Body | Response |
|-----------|-------|-------------|--------------|----------|
| POST | `/api/orders` | Create new order | CreateOrderCommand | 201 Created |
| GET | `/api/orders/{id}` | Get order by ID | - | 200 OK / 404 Not Found |
| GET | `/api/orders` | Get paginated orders | Query params | 200 OK |
| PUT | `/api/orders/{id}` | Update existing order | UpdateOrderCommand | 200 OK / 404 Not Found |
| DELETE | `/api/orders/{id}` | Soft delete order | - | 204 No Content / 404 Not Found |

### Response Status Codes

**Success Responses:**
- `200 OK` - Successful GET/PUT operations
- `201 Created` - Successful POST operations
- `204 No Content` - Successful DELETE operations

**Client Error Responses:**
- `400 Bad Request` - Validation errors, malformed requests
- `404 Not Found` - Resource not found
- `409 Conflict` - Business rule violations

**Server Error Responses:**
- `500 Internal Server Error` - Unhandled exceptions

### Pagination Implementation

**Query Parameters:**
- `pageNumber` (default: 1)
- `pageSize` (default: 20, max: 100)
- `customerId` (optional filter)

**Response Structure:**
```json
{
  "orders": [...],
  "totalCount": 150,
  "pageNumber": 1,
  "pageSize": 20,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

---

## Message Integration Strategy

### MassTransit Configuration for API Integration

**Enhanced Message Publishing:**
```csharp
public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, CreateOrderResult>
{
    private readonly IOrderRepository _repository;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IMapper _mapper;

    public async Task<CreateOrderResult> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        // Create order entity
        var order = _mapper.Map<Order>(request);
        
        // Save to database
        await _repository.AddAsync(order, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        // Publish domain events
        foreach (var domainEvent in order.DomainEvents)
        {
            await _publishEndpoint.Publish(domainEvent, cancellationToken);
        }
        
        order.ClearDomainEvents();

        return _mapper.Map<CreateOrderResult>(order);
    }
}
```

**Message Correlation:**
- Use correlation IDs from HTTP headers
- Propagate trace context to messages
- Include user context in message headers

---

## Observability Setup

### OpenTelemetry Configuration for API Endpoints

**Enhanced Instrumentation:**
```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("WorkerService-API")
        .AddAttributes(new Dictionary<string, object>
        {
            ["service.version"] = "1.0.0",
            ["api.version"] = "v1"
        }))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(options =>
        {
            options.EnrichWithHttpRequest = (activity, request) =>
            {
                activity.SetTag("http.route", request.Path);
                activity.SetTag("user.id", request.Headers["X-User-Id"].ToString());
            };
            options.EnrichWithHttpResponse = (activity, response) =>
            {
                activity.SetTag("http.response.status_code", response.StatusCode);
            };
        })
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSqlClientInstrumentation()
        .AddSource("MassTransit")
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter("MassTransit")
        .AddMeter("OrdersAPI")
        .AddPrometheusExporter());
```

### Custom Metrics for API Operations

```csharp
public static class OrderApiMetrics
{
    private static readonly Meter _meter = new("OrdersAPI", "1.0.0");
    
    public static readonly Counter<int> OrdersCreated = _meter.CreateCounter<int>("orders.created.total");
    public static readonly Counter<int> OrdersUpdated = _meter.CreateCounter<int>("orders.updated.total");
    public static readonly Counter<int> OrdersDeleted = _meter.CreateCounter<int>("orders.deleted.total");
    public static readonly Histogram<double> OrderCreationDuration = _meter.CreateHistogram<double>("order.creation.duration");
}
```

---

## Validation Gates (Executable Commands)

### Project Structure Validation
```bash
# Verify Clean Architecture project structure
dotnet sln list | grep -E "Domain|Application|Infrastructure|Worker"

# Verify project references
dotnet list src/WorkerService.Worker/ reference | grep -E "Application|Infrastructure"
dotnet list src/WorkerService.Application/ reference | grep Domain
dotnet list src/WorkerService.Infrastructure/ reference | grep -E "Application|Domain"
```

### SDK and Package Validation
```bash
# Verify project SDK configuration
grep "Microsoft.NET.Sdk.Web" src/WorkerService.Worker/WorkerService.Worker.csproj

# Verify required packages
dotnet list src/WorkerService.Worker/ package | grep -E "OpenTelemetry|OpenApi|Swashbuckle"
dotnet list src/WorkerService.Application/ package | grep -E "AutoMapper|MediatR|FluentValidation"
```

### Build and Test Validation
```bash
# Restore and build solution
dotnet restore
dotnet build --no-restore --verbosity minimal

# Run unit tests
dotnet test --no-build --verbosity minimal

# Verify no build warnings or errors
dotnet build 2>&1 | grep -E "warning|error" || echo "Build successful"
```

### API Endpoint Validation
```bash
# Start application in background
dotnet run --project src/WorkerService.Worker &
APP_PID=$!

# Wait for startup
sleep 10

# Test health endpoints
curl -f http://localhost:5000/health || echo "Health check failed"

# Test OpenAPI endpoint
curl -f http://localhost:5000/openapi/v1.json || echo "OpenAPI endpoint failed"

# Test Swagger UI (if configured)
curl -f http://localhost:5000/swagger/index.html || echo "Swagger UI not accessible"

# Test CRUD endpoints
echo "Testing Orders API endpoints..."

# Create order
ORDER_RESPONSE=$(curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "test-customer",
    "items": [
      {
        "productId": "product-1",
        "quantity": 2,
        "unitPrice": 29.99
      }
    ]
  }' -w "%{http_code}" -o /tmp/order_response.json)

if [ "$ORDER_RESPONSE" = "201" ]; then
    ORDER_ID=$(jq -r '.orderId' /tmp/order_response.json)
    echo "Order created successfully: $ORDER_ID"
    
    # Get order by ID
    curl -f "http://localhost:5000/api/orders/$ORDER_ID" || echo "Get order failed"
    
    # Get paginated orders
    curl -f "http://localhost:5000/api/orders?pageNumber=1&pageSize=10" || echo "Get orders failed"
else
    echo "Order creation failed with HTTP $ORDER_RESPONSE"
fi

# Stop application
kill $APP_PID
```

### OpenTelemetry Validation
```bash
# Verify OpenTelemetry configuration
grep -r "AddOpenTelemetry" src/WorkerService.Worker/
grep -r "AddAspNetCoreInstrumentation" src/WorkerService.Worker/
grep -r "AddMeter" src/WorkerService.Worker/

# Check for required instrumentation
grep -r "AddEntityFrameworkCoreInstrumentation\|AddHttpClientInstrumentation\|AddSqlClientInstrumentation" src/WorkerService.Worker/
```

### Clean Architecture Compliance Validation
```bash
# Domain layer should have no external dependencies
DOMAIN_REFS=$(dotnet list src/WorkerService.Domain/ reference 2>/dev/null | wc -l)
if [ "$DOMAIN_REFS" -eq 0 ]; then
    echo "✅ Domain layer has no dependencies (Clean Architecture compliance)"
else
    echo "❌ Domain layer has external dependencies (violates Clean Architecture)"
fi

# Application layer should only depend on Domain
APP_REFS=$(dotnet list src/WorkerService.Application/ reference | grep -v Domain | wc -l)
if [ "$APP_REFS" -eq 0 ]; then
    echo "✅ Application layer only depends on Domain"
else
    echo "❌ Application layer has non-Domain dependencies"
fi

# Infrastructure should not be referenced by Domain or Application
DOMAIN_INFRA=$(grep -r "WorkerService.Infrastructure" src/WorkerService.Domain/ || echo "none")
APP_INFRA=$(grep -r "WorkerService.Infrastructure" src/WorkerService.Application/ || echo "none")

if [[ "$DOMAIN_INFRA" == "none" && "$APP_INFRA" == "none" ]]; then
    echo "✅ Clean Architecture boundaries maintained"
else
    echo "❌ Infrastructure layer improperly referenced"
fi
```

### Database Migration Validation
```bash
# Check for pending migrations
dotnet ef migrations list --project src/WorkerService.Infrastructure --startup-project src/WorkerService.Worker

# Validate database schema
dotnet ef database update --project src/WorkerService.Infrastructure --startup-project src/WorkerService.Worker --dry-run
```

### Message Publishing Validation
```bash
# Start application with message logging
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/WorkerService.Worker &
APP_PID=$!

# Create order and check logs for message publishing
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -d '{
    "customerId": "test-customer", 
    "items": [{"productId": "test", "quantity": 1, "unitPrice": 10.00}]
  }'

# Check logs for MassTransit message publishing
sleep 5
kill $APP_PID

# Verify no exceptions in logs
grep -i "exception\|error" logs/* || echo "No exceptions found in logs"
```

---

## Testing Strategy

### Unit Testing Structure

**Controller Tests:**
```csharp
public class OrdersControllerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<IValidator<CreateOrderCommand>> _validatorMock;
    private readonly OrdersController _controller;

    [Fact]
    public async Task CreateOrder_WithValidCommand_ReturnsCreatedResult()
    {
        // Arrange
        var command = new CreateOrderCommand("customer-1", new List<OrderItemDto>());
        var result = new CreateOrderResult(Guid.NewGuid(), "customer-1", 100m, DateTime.UtcNow);
        
        _validatorMock.Setup(x => x.ValidateAsync(command, default))
                     .ReturnsAsync(new ValidationResult());
        _mediatorMock.Setup(x => x.Send(command, default))
                     .ReturnsAsync(result);

        // Act
        var response = await _controller.CreateOrder(command, default);

        // Assert
        var createdResult = Assert.IsType<CreatedAtActionResult>(response);
        Assert.Equal(201, createdResult.StatusCode);
    }
}
```

**Handler Tests:**
```csharp
public class CreateOrderCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidCommand_CreatesOrderAndPublishesEvent()
    {
        // Test command handler logic in isolation
        // Verify repository calls
        // Verify message publishing
        // Verify domain event handling
    }
}
```

### Integration Testing

**API Integration Tests:**
```csharp
public class OrdersApiIntegrationTests : IClassFixture<TestWebApplicationFactory<Program>>
{
    [Fact]
    public async Task CreateOrder_EndToEnd_CreatesOrderAndPublishesMessage()
    {
        // Test full HTTP request/response cycle
        // Verify database persistence
        // Verify message publishing
        // Verify OpenTelemetry traces
    }
}
```

### Performance Testing Considerations

**Load Testing Scenarios:**
- Concurrent order creation requests
- Large pagination queries
- Message publishing throughput
- Database connection pooling efficiency

---

## Production Considerations

### Error Handling Strategy

**Global Exception Handling:**
```csharp
public class GlobalExceptionMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (ValidationException ex)
        {
            await HandleValidationException(context, ex);
        }
        catch (NotFoundException ex)
        {
            await HandleNotFoundException(context, ex);
        }
        catch (Exception ex)
        {
            await HandleGenericException(context, ex);
        }
    }
}
```

**Structured Error Responses:**
```csharp
public class ApiErrorResponse
{
    public string Type { get; set; }
    public string Title { get; set; }
    public int Status { get; set; }
    public string TraceId { get; set; }
    public Dictionary<string, object> Errors { get; set; }
}
```

### Security Considerations

**Input Validation:**
- FluentValidation for all API inputs
- SQL injection prevention via EF Core parameterization
- Request size limitations
- Rate limiting implementation

**Authentication & Authorization:**
```csharp
// Future enhancement for authentication
builder.Services.AddAuthentication()
    .AddJwtBearer(options => {
        // JWT configuration
    });

// Role-based authorization
[Authorize(Roles = "OrderManager")]
public class OrdersController : ControllerBase
```

### Performance Optimization

**Caching Strategy:**
```csharp
// Redis caching for frequently accessed orders
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
});

// Memory caching for lookup data
builder.Services.AddMemoryCache();
```

**Database Optimization:**
- Connection pooling with `AddDbContextPool`
- Proper indexing on frequently queried columns
- Async operations throughout the stack
- Pagination to prevent large result sets

### Monitoring and Alerting

**Key Metrics to Monitor:**
- API response times
- Error rates by endpoint
- Database connection pool health
- Message publishing success/failure rates
- Order creation/update throughput

**Alerting Thresholds:**
- Response time > 2 seconds
- Error rate > 5%
- Database connection pool exhaustion
- Message publishing failures

### Deployment Strategy

**Container Configuration:**
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/out .
EXPOSE 8080
ENTRYPOINT ["dotnet", "WorkerService.Worker.dll"]
```

**Environment Configuration:**
```yaml
# docker-compose.yml
version: '3.8'
services:
  worker-api:
    build: .
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_URLS=http://+:8080
      - OTEL_EXPORTER_OTLP_ENDPOINT=http://jaeger:14268
    depends_on:
      - postgres
      - rabbitmq
```

---

## Risk Assessment and Mitigation

### Technical Risks

**Risk: Database Performance Degradation**
- **Likelihood**: Medium
- **Impact**: High
- **Mitigation**: Implement connection pooling, proper indexing, query optimization, monitoring

**Risk: Message Publishing Failures**
- **Likelihood**: Medium  
- **Impact**: Medium
- **Mitigation**: Implement retry policies, dead letter queues, circuit breaker patterns

**Risk: API Endpoint Security Vulnerabilities**
- **Likelihood**: Medium
- **Impact**: High
- **Mitigation**: Input validation, rate limiting, authentication/authorization, security testing

### Operational Risks

**Risk: Increased Infrastructure Complexity**
- **Likelihood**: High
- **Impact**: Medium
- **Mitigation**: Comprehensive documentation, infrastructure as code, monitoring

**Risk: Backwards Compatibility Issues**
- **Likelihood**: Low
- **Impact**: High
- **Mitigation**: Maintain existing background service functionality, versioned APIs

### Mitigation Strategies

**Development Phase:**
- Comprehensive unit and integration testing
- Code review process with architecture compliance checks
- Performance testing under load
- Security vulnerability scanning

**Deployment Phase:**
- Blue-green deployment strategy
- Health check monitoring
- Rollback procedures
- Gradual traffic shifting

**Operations Phase:**
- 24/7 monitoring and alerting
- Regular performance reviews
- Capacity planning
- Incident response procedures

---

## Implementation Timeline

### Week 1: Foundation
- [ ] Package installation and configuration
- [ ] Project structure verification
- [ ] Basic API endpoint scaffolding
- [ ] OpenAPI configuration

### Week 2: Core Implementation
- [ ] CQRS command/query handlers
- [ ] FluentValidation validators
- [ ] AutoMapper configurations
- [ ] Repository enhancements

### Week 3: API Development
- [ ] OrdersController implementation
- [ ] Error handling middleware
- [ ] Message publishing integration
- [ ] OpenTelemetry enhancement

### Week 4: Testing & Documentation
- [ ] Unit test implementation
- [ ] Integration test development
- [ ] API documentation completion
- [ ] Performance testing

### Week 5: Production Readiness
- [ ] Security review and implementation
- [ ] Performance optimization
- [ ] Monitoring and alerting setup
- [ ] Deployment preparation

---

## Quality Assurance Checklist

### Code Quality
- [ ] All code follows Clean Architecture principles
- [ ] SOLID principles applied throughout
- [ ] Comprehensive error handling implemented
- [ ] Logging and tracing configured
- [ ] Input validation on all endpoints

### Testing Coverage
- [ ] Unit tests for all business logic (>90% coverage)
- [ ] Integration tests for all API endpoints
- [ ] Performance tests for critical paths
- [ ] Security testing completed

### Documentation
- [ ] OpenAPI specification complete and accurate
- [ ] Code comments for complex business logic
- [ ] Deployment and configuration guide
- [ ] Troubleshooting documentation

### Production Readiness
- [ ] Health checks implemented and tested
- [ ] Monitoring and alerting configured
- [ ] Performance benchmarks established
- [ ] Security review completed
- [ ] Backup and recovery procedures tested

---

## Success Criteria

### Functional Requirements
- [x] Order entity with business rules exists in Domain layer
- [ ] Complete CQRS command/query handlers in Application layer  
- [ ] FluentValidation validators for all API inputs
- [ ] OrdersController with full CRUD operations (POST, GET, PUT, DELETE)
- [ ] Database migrations for Order/OrderItem tables
- [ ] AutoMapper profiles for entity-DTO conversion
- [ ] Native OpenAPI documentation accessible at `/openapi/v1.json`
- [ ] Swagger UI available at `/swagger` (development only)

### Non-Functional Requirements
- [ ] All endpoints return appropriate HTTP status codes (200, 201, 400, 404, 500)
- [ ] Pagination implemented for GET `/api/orders` with metadata
- [ ] Input validation provides detailed error messages
- [ ] MassTransit messages published for order events
- [ ] OpenTelemetry traces and metrics for all API operations
- [ ] Health checks include API availability monitoring
- [ ] RESTful conventions followed for all endpoints

### Quality Assurance
- [ ] Integration tests cover all CRUD operations (>90% code coverage)
- [ ] Performance benchmarks under expected load
- [ ] Security scanning with no high-severity vulnerabilities
- [ ] Clean Architecture boundaries maintained and verified

### Production Readiness
- [ ] Structured logging with correlation IDs
- [ ] Comprehensive error handling and user-friendly error messages
- [ ] Monitoring dashboards for key metrics
- [ ] Deployment automation and rollback procedures
- [ ] Documentation complete for operations team

---

## Conclusion

This PRP provides a comprehensive roadmap for implementing production-ready CRUD API endpoints in a .NET 9 Worker Service while maintaining strict Clean Architecture principles. The implementation leverages the latest .NET 9 patterns including native OpenAPI support, hybrid worker-web hosting, and modern observability practices.

The detailed validation gates ensure successful implementation, while the production considerations address real-world deployment scenarios. The phased approach minimizes risk while delivering incremental value.

**Expected Outcome**: A robust, scalable, and maintainable API layer that seamlessly integrates with existing background processing capabilities, providing a unified platform for order management operations.