# .NET 9 Worker Service - Context Engineering Rules

This file contains the domain-specific rules for .NET 9 Worker Service development with Clean Architecture, MassTransit, PostgreSQL, and OpenTelemetry.

## 🔄 Context Engineering Core Principles

**IMPORTANT: These principles apply to ALL .NET Worker Service development:**

### PRP Framework Workflow
- **Always read features under FRs/** - Check existing feature requests before generating a new PRP
- **Use the PRP pattern**: new-feature-request.md → `/generate-worker-prp new-feature-request.md` → `/execute-worker-prp PRPs/filename.md`
- **Follow validation loops** - Each PRP must include executable validation steps
- **Context is King** - Include ALL necessary documentation, examples, and patterns

### Research Methodology
- **Web search first** - Always research latest .NET 9, MassTransit, and EF Core patterns
- **Documentation deep dive** - Study official Microsoft docs, MassTransit docs, and PostgreSQL guides
- **Pattern extraction** - Identify Clean Architecture and CQRS patterns
- **Gotcha documentation** - Document common pitfalls in Worker Services and messaging

## 🏗️ Clean Architecture Enforcement

### Layer Dependencies (CRITICAL)
- **Domain Layer**: No dependencies on anything external (POCO only)
- **Application Layer**: Depends ONLY on Domain (contains CQRS handlers, validation)
- **Infrastructure Layer**: Depends on Application and Domain (data access, messaging, external services)
- **Worker Layer**: Depends on Infrastructure and Application (hosts and consumers)

### Project Structure Standards
```
src/
├── WorkerService.Domain/          # Entities, Value Objects, Interfaces
├── WorkerService.Application/     # CQRS Handlers, Services, DTOs
├── WorkerService.Infrastructure/  # EF Context, Repositories, MassTransit
└── WorkerService.Worker/          # BackgroundServices, Program.cs, Health Checks
```

### Anti-Patterns (NEVER DO)
- ❌ Reference Infrastructure from Domain
- ❌ Put business logic in Infrastructure layer
- ❌ Bypass Application layer from Worker
- ❌ Create circular dependencies between projects
- ❌ Do not use AutoMapper or any other mapping libraries. Do manual mapping instead.

## 🚌 MassTransit Integration Standards

### Consumer Implementation
- **Always implement IConsumer<T>** for message handling
- **Use proper DI registration** with `AddMassTransit()` and `UsingRabbitMq()`
- **Configure quorum queues** for reliability: `e.SetQuorumQueue(3)`
- **Implement idempotent consumers** to handle duplicate messages safely

### Configuration Patterns
```csharp
// Always use this pattern for MassTransit configuration
services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedConsumer>();
    
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq://localhost", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });
        
        cfg.ReceiveEndpoint("order-created", e =>
        {
            e.SetQuorumQueue(3); // Reliability
            e.ConfigureConsumer<OrderCreatedConsumer>(context);
        });
    });
});
```

### Observability Integration
- **Always enable MassTransit OpenTelemetry**: `.AddSource("MassTransit")`
- **Configure metrics**: `.AddMeter("MassTransit")`
- **Use correlation IDs** for message tracing

## 💾 Database Integration with EF Core

### DbContext Lifetime Management (CRITICAL)
- **NEVER inject DbContext directly into singleton services**
- **Always use IServiceScopeFactory** in BackgroundService implementations
- **Create and dispose scopes for each unit of work**

### Required Pattern for Background Services
```csharp
public class OrderProcessingService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            // Your work here
            
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
```

### Repository Pattern Implementation
- **Always use Repository<T> pattern** for data access abstraction
- **Implement IRepository<T> in Domain** layer as interface
- **Implement concrete repositories in Infrastructure** layer
- **Use async/await patterns** throughout data access

### PostgreSQL Configuration
```csharp
// Always use this pattern for PostgreSQL configuration
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
```

## 📊 OpenTelemetry Observability

### Required Instrumentations
- **ASP.NET Core**: `AddAspNetCoreInstrumentation()`
- **HTTP Client**: `AddHttpClientInstrumentation()`
- **Entity Framework**: `AddEntityFrameworkCoreInstrumentation()`
- **SQL Client**: `AddSqlClientInstrumentation()`
- **MassTransit**: `AddSource("MassTransit")`

### Configuration Pattern
```csharp
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("YourServiceName"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSqlClientInstrumentation()
        .AddSource("MassTransit")
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter("MassTransit")
        .AddOtlpExporter());
```

### Environment Variable Configuration
- **Use OTEL_EXPORTER_OTLP_ENDPOINT** for exporter configuration
- **Use OTEL_SERVICE_NAME** for service identification
- **Use OTEL_RESOURCE_ATTRIBUTES** for additional metadata

## 🏥 Health Check Implementation

### Required Health Checks
- **RabbitMQ**: Use `AspNetCore.HealthChecks.Rabbitmq`
- **PostgreSQL**: Use `AspNetCore.HealthChecks.NpgSql`
- **Custom Worker**: Implement `IHealthCheck` for worker status

### Configuration Pattern
```csharp
// Target Microsoft.NET.Sdk.Web for minimal API support
builder.Services.AddHealthChecks()
    .AddRabbitMQ(connectionString: "amqp://guest:guest@localhost:5672/")
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection"))
    .AddCheck<WorkerHealthCheck>("worker");

var app = builder.Build();
app.MapHealthChecks("/health");
```

## 🧪 Testing Standards

### Unit Testing
- **Always create unit tests** for domain entities and application handlers
- **Use xUnit** as the testing framework
- **Mock external dependencies** using Moq or NSubstitute
- **Test business logic in isolation**

### Integration Testing
- **Use MassTransit.TestFramework** for message testing
- **Use in-memory database** for data access testing
- **Test end-to-end workflows** with test harness

### Test Structure
```
tests/
├── ProjectName.UnitTests/       # Domain and Application layer tests
└── ProjectName.IntegrationTests/ # Full workflow and consumer tests
```

## 🐳 Development Environment

### Docker Compose Requirements
- **RabbitMQ**: Include management UI on port 15672
- **PostgreSQL**: Include initialization scripts
- **Jaeger**: For trace visualization
- **Prometheus**: For metrics collection

### Required Services
```yaml
version: '3.8'
services:
  rabbitmq:
    image: rabbitmq:3-management
    ports:
      - "5672:5672"
      - "15672:15672"
  
  postgres:
    image: postgres:15
    environment:
      POSTGRES_DB: workerservice
      POSTGRES_USER: worker
      POSTGRES_PASSWORD: password
    ports:
      - "5432:5432"
```

## 📦 Package Management

### Required NuGet Packages
**Domain Project**: No external dependencies (POCO only)

**Application Project**:
- MediatR
- FluentValidation
- Microsoft.Extensions.Logging.Abstractions

**Infrastructure Project**:
- Npgsql.EntityFrameworkCore.PostgreSQL (version 9.0.4+)
- MassTransit.RabbitMQ
- MassTransit.EntityFrameworkCore

**Worker Project**:
- Microsoft.Extensions.Hosting
- OpenTelemetry.Extensions.Hosting
- OpenTelemetry.Instrumentation.AspNetCore
- OpenTelemetry.Instrumentation.Http
- OpenTelemetry.Instrumentation.EntityFrameworkCore
- OpenTelemetry.Instrumentation.SqlClient
- OpenTelemetry.Exporter.OpenTelemetryProtocol
- AspNetCore.HealthChecks.Rabbitmq
- AspNetCore.HealthChecks.NpgSql

## 🔒 Security Standards

### Connection String Management
- **Use User Secrets** for local development: `dotnet user-secrets`
- **Use environment variables** for production deployment
- **Never commit connection strings** to source control

### Message Security
- **Use correlation IDs** for message tracing
- **Validate all incoming messages** with FluentValidation
- **Implement circuit breaker patterns** for external service calls

## 🚀 Deployment Considerations

### Production Readiness
- **Implement graceful shutdown** with CancellationToken
- **Use structured logging** with correlation IDs
- **Configure retry policies** for transient failures
- **Monitor resource usage** with health checks

### Performance Optimization
- **Use connection pooling** for database access
- **Configure prefetch counts** for MassTransit consumers
- **Enable batch publishing** for message throughput
- **Use async/await** throughout the application

## 🚫 Common Gotchas and Anti-Patterns

### DbContext Pitfalls
- ❌ Don't share DbContext across threads
- ❌ Don't inject DbContext into singleton services
- ❌ Don't forget to dispose scopes in background services
- ❌ Don't ignore migration strategies

### MassTransit Pitfalls
- ❌ Don't configure unlimited prefetch
- ❌ Don't forget idempotency in consumers
- ❌ Don't ignore message acknowledgment patterns
- ❌ Don't skip correlation ID configuration

### Worker Service Pitfalls
- ❌ Don't ignore cancellation tokens
- ❌ Don't block async operations with .Result
- ❌ Don't forget error handling and retry logic
- ❌ Don't skip health check implementation

### Observability Pitfalls
- ❌ Don't hardcode telemetry endpoints
- ❌ Don't forget service metadata
- ❌ Don't ignore performance impact of instrumentation
- ❌ Don't skip environment variable configuration

## 📝 Code Quality Standards

### CQRS Implementation
- **Commands** for state-changing operations
- **Queries** for read-only operations
- **Handlers** must be in Application layer
- **Use MediatR** for request/response patterns

### Error Handling
- **Use Result patterns** for operation outcomes
- **Implement global exception handling**
- **Log structured errors** with correlation IDs
- **Use circuit breaker patterns** for resilience

### Code Style
- **Follow C# naming conventions**
- **Use async/await** for I/O operations
- **Implement cancellation token support**
- **Write XML documentation** for public APIs

These rules ensure consistent, maintainable, and production-ready .NET Worker Service implementations following Clean Architecture principles with comprehensive observability and testing.