# .NET 9 Worker Service - Order Processing System

This is an example project to test PRP AI Workflow with Claude Code.

A production-ready .NET 9 Worker Service application implementing an order processing system with Clean Architecture, MassTransit messaging, PostgreSQL persistence, and OpenTelemetry observability.

## 🎯 Application Overview

This Worker Service demonstrates a complete order processing workflow featuring:

- **Order Lifecycle Management**: Create, validate, process payments, and ship orders
- **Background Processing**: Automated order processing with configurable intervals
- **Message-Driven Architecture**: Event-driven communication using MassTransit
- **Flexible Development**: Configurable in-memory dependencies for rapid local development
- **Production Observability**: Comprehensive monitoring with OpenTelemetry and health checks

## 🏗️ Architecture & Design

### Clean Architecture Implementation

The application follows Clean Architecture principles with strict dependency flow:

```
┌─────────────────┐    ┌──────────────────────┐    ┌─────────────────────┐    ┌─────────────────┐
│     Domain      │←───│    Application       │←───│   Infrastructure    │←───│     Worker      │
│                 │    │                      │    │                     │    │                 │
│ • Order Entity  │    │ • CQRS Handlers      │    │ • EF Core Context   │    │ • BackgroundSvc │
│ • OrderStatus   │    │ • MediatR Commands   │    │ • Repositories      │    │ • Health Checks │
│ • Money Value   │    │ • FluentValidation   │    │ • MassTransit       │    │ • Program.cs    │
│ • Domain Events │    │ • Business Services  │    │ • Consumers         │    │ • Configuration │
│ • Interfaces    │    │ • DTOs & Mappings    │    │ • External Services │    │ • Startup Logic │
└─────────────────┘    └──────────────────────┘    └─────────────────────┘    └─────────────────┘
    No Dependencies         Depends on Domain        Depends on Application      Depends on Infrastructure
```

### Key Design Patterns

- **CQRS with MediatR**: Separate command and query handling
- **Repository Pattern**: Clean data access abstraction  
- **Domain Events**: Decoupled business logic communication
- **Background Services**: Reliable background processing with proper scoping
- **Event Sourcing**: Order state transitions tracked via domain events

## 📁 Project Structure

```
dotnet9-worker-service/
├── src/                                   # Application source code
│   ├── WorkerService.Domain/             # 🔵 Domain Layer (no dependencies)
│   │   ├── Entities/
│   │   │   ├── Order.cs                  # Order aggregate with business logic
│   │   │   ├── OrderItem.cs              # Order line items
│   │   │   └── OrderStatus.cs            # Order state enumeration
│   │   ├── ValueObjects/
│   │   │   └── Money.cs                  # Money value object with validation
│   │   ├── Events/
│   │   │   ├── IDomainEvent.cs           # Domain event interface
│   │   │   └── OrderEvents.cs            # Order-related domain events
│   │   └── Interfaces/
│   │       ├── IRepository.cs            # Generic repository interface
│   │       └── IOrderRepository.cs       # Order-specific repository contract
│   │
│   ├── WorkerService.Application/        # 🟡 Application Layer (depends on Domain)
│   │   ├── Commands/
│   │   │   └── CreateOrderCommand.cs     # Order creation command
│   │   ├── Queries/
│   │   │   └── GetOrderQuery.cs          # Order retrieval query
│   │   ├── Handlers/
│   │   │   ├── CreateOrderCommandHandler.cs  # CQRS command handler
│   │   │   └── GetOrderQueryHandler.cs   # CQRS query handler
│   │   └── Validators/
│   │       └── CreateOrderCommandValidator.cs # FluentValidation rules
│   │
│   ├── WorkerService.Infrastructure/     # 🟠 Infrastructure Layer (depends on Application)
│   │   ├── Data/
│   │   │   └── ApplicationDbContext.cs   # EF Core DbContext with entity configuration
│   │   ├── Repositories/
│   │   │   ├── Repository.cs             # Generic repository implementation
│   │   │   └── OrderRepository.cs        # Order repository with business queries
│   │   └── Consumers/
│   │       └── OrderCreatedConsumer.cs   # MassTransit message consumer
│   │
│   └── WorkerService.Worker/             # 🔴 Worker Layer (depends on Infrastructure)
│       ├── Configuration/
│       │   └── InMemorySettings.cs       # In-memory provider configuration
│       ├── Services/
│       │   ├── OrderProcessingService.cs # Background order processing
│       │   └── MetricsCollectionService.cs # Application metrics collection
│       ├── Health/
│       │   └── WorkerHealthCheck.cs      # Custom health check implementation
│       ├── Program.cs                    # Application entry point & DI configuration
│       ├── appsettings.json              # Production configuration
│       └── appsettings.Development.json  # Development configuration (in-memory enabled)
│
├── tests/                                # Test projects
│   ├── WorkerService.UnitTests/          # Unit tests for domain logic
│   │   └── Domain/
│   │       └── OrderTests.cs             # Order entity behavior tests
│   └── WorkerService.IntegrationTests/   # Integration and system tests
│       ├── Tests/
│       │   ├── InMemoryConfigurationTests.cs    # In-memory provider tests
│       │   ├── BackgroundServiceIntegrationTests.cs # Worker service tests
│       │   ├── HealthCheckIntegrationTests.cs   # Health monitoring tests
│       │   ├── MessageFlowIntegrationTests.cs   # MassTransit integration tests
│       │   └── OrderProcessingIntegrationTests.cs # End-to-end workflow tests
│       ├── Fixtures/                     # Test infrastructure
│       └── Utilities/                    # Test helpers and builders
│
├── docker-compose.yml                    # Development environment (PostgreSQL, RabbitMQ, observability)
├── WorkerService.sln                     # Solution file
├── CLAUDE.md                             # Development context and architectural rules
└── README.md                             # This documentation
```

## 🚀 Technology Stack

### Core Framework
- **.NET 9**: Latest long-term support version with performance improvements
- **ASP.NET Core Minimal APIs**: For health check endpoints and lightweight HTTP interface
- **Worker Services**: BackgroundService implementation for long-running processes

### Clean Architecture Implementation
- **MediatR 12.x**: CQRS pattern implementation with request/response pipelines
- **FluentValidation**: Declarative validation rules with clean error handling
- **Domain Events**: Decoupled business logic communication

### Data & Messaging
- **Entity Framework Core 9**: Code-first PostgreSQL integration with advanced features
- **MassTransit 8.5**: Production-ready message bus with RabbitMQ transport
- **PostgreSQL 15**: Primary data store with JSON support and performance optimizations

### Observability & Monitoring
- **OpenTelemetry**: Distributed tracing, metrics collection, and correlation
- **Serilog**: Structured logging with contextual information
- **Health Checks**: Dependency monitoring and application status reporting
- **Prometheus**: Metrics export for monitoring dashboards

### Development & Testing
- **In-Memory Providers**: Configurable in-memory database and message bus for fast development
- **xUnit**: Comprehensive unit and integration testing framework
- **MassTransit Test Framework**: Message-based integration testing
- **Testcontainers**: Isolated integration testing with Docker

## 💻 Local Development

### Quick Start Options

#### Option 1: Full In-Memory Development (No Docker Required)
```bash
# Run with in-memory database and message broker
dotnet run --project src/WorkerService.Worker/ --environment Development

# Or explicitly set environment variables
InMemory__UseDatabase=true InMemory__UseMessageBroker=true dotnet run --project src/WorkerService.Worker/
```
**Benefits**: Instant startup, no external dependencies, perfect for rapid iteration

#### Option 2: Production-Like Environment (With Docker)
```bash
# Start infrastructure services
docker-compose up -d

# Run application against real dependencies  
dotnet run --project src/WorkerService.Worker/
```
**Benefits**: Full production simulation, persistent data, real message queuing

#### Option 3: Mixed Development (Hybrid Approach)
```bash
# Use in-memory database but real RabbitMQ
InMemory__UseDatabase=true docker-compose up -d rabbitmq
dotnet run --project src/WorkerService.Worker/

# Or in-memory messaging but real PostgreSQL
InMemory__UseMessageBroker=true docker-compose up -d postgres
dotnet run --project src/WorkerService.Worker/
```

### Configuration Options

The application supports flexible configuration through environment variables:

| Environment Variable | Default | Description |
|---------------------|---------|-------------|
| `InMemory__UseDatabase` | `false` | Use in-memory database instead of PostgreSQL |
| `InMemory__UseMessageBroker` | `false` | Use in-memory transport instead of RabbitMQ |
| `HealthChecks__Enabled` | `false` | Enable health check endpoints |

### Development Environment Setup

1. **Prerequisites**
   ```bash
   # Install .NET 9 SDK
   dotnet --version  # Should show 9.x.x
   
   # Optional: Docker for full environment
   docker --version
   docker-compose --version
   ```

2. **Build & Test**
   ```bash
   # Restore dependencies
   dotnet restore
   
   # Build solution
   dotnet build
   
   # Run tests
   dotnet test
   ```

3. **Database Migrations** (when using PostgreSQL)
   ```bash
   # Create migration
   dotnet ef migrations add InitialCreate --project src/WorkerService.Infrastructure --startup-project src/WorkerService.Worker
   
   # Update database
   dotnet ef database update --project src/WorkerService.Infrastructure --startup-project src/WorkerService.Worker
   ```

## 📦 Order Processing Workflow

### Order Lifecycle States

The application implements a complete order processing pipeline with the following states:

```
┌─────────┐    ┌───────────┐    ┌──────────────────┐    ┌──────┐    ┌─────────┐    ┌───────────┐
│ Pending │───▶│ Validated │───▶│ PaymentProcessing │───▶│ Paid │───▶│ Shipped │───▶│ Delivered │
└─────────┘    └───────────┘    └──────────────────┘    └──────┘    └─────────┘    └───────────┘
     │                                                                                      ▲
     │         ┌───────────┐                                                               │
     └────────▶│ Cancelled │◀──────────────────────────────────────────────────────────────┘
               └───────────┘
```

### Simulating Order Processing

#### 1. Create Orders (Using MediatR Commands)

```bash
# Start the application
dotnet run --project src/WorkerService.Worker/ --environment Development

# In another terminal, you can create orders programmatically:
```

```csharp
// Example: Creating an order through the application
var orderItems = new[]
{
    new OrderItem("LAPTOP-001", 1, new Money(1299.99m)),
    new OrderItem("MOUSE-001", 1, new Money(49.99m))
};

var command = new CreateOrderCommand
{
    CustomerId = "CUSTOMER-123",
    Items = orderItems
};

// This creates an order in Pending status
var result = await mediator.Send(command);
```

#### 2. Monitor Background Processing

The `OrderProcessingService` automatically:

- **Polls for pending orders** every 30 seconds (in-memory mode) or 5 minutes (production)
- **Validates orders** that are within the processing window (30 minutes)
- **Cancels orders** that have been pending too long
- **Logs processing activities** with structured information

**Watch the logs:**
```bash
[2024-01-15 10:30:00 INF] OrderProcessingService starting with configuration: Database: In-Memory, MessageBroker: In-Memory
[2024-01-15 10:30:30 DBG] Checking for pending orders to process
[2024-01-15 10:30:30 DBG] Order 123e4567-e89b-12d3-a456-426614174000 is still within processing window
[2024-01-15 10:31:00 WRN] Order 456e7890-e89b-12d3-a456-426614174001 has been pending for too long, cancelling
```

#### 3. Observe Domain Events

When orders change state, domain events are automatically raised:

- `OrderCreatedEvent` → Triggers `OrderCreatedConsumer`
- `OrderValidatedEvent` → Could trigger inventory checks
- `OrderPaidEvent` → Could trigger shipping workflows
- `OrderCancelledEvent` → Could trigger refund processes

#### 4. Manual Order State Transitions

```csharp
// Get order from repository
var order = await orderRepository.GetByIdAsync(orderId);

// Progress through states
order.ValidateOrder();           // Pending → Validated
order.MarkAsPaymentProcessing(); // Validated → PaymentProcessing  
order.MarkAsPaid();             // PaymentProcessing → Paid
order.MarkAsShipped();          // Paid → Shipped
order.MarkAsDelivered();        // Shipped → Delivered

// Or cancel at any point (except Delivered)
order.Cancel();                 // Any State → Cancelled

await orderRepository.UpdateAsync(order);
```

### Testing the Complete Workflow

#### Integration Test Example
```bash
# Run integration tests that simulate the complete workflow
dotnet test --filter "OrderProcessingIntegrationTests" --logger "console;verbosity=detailed"
```

The integration tests demonstrate:
- Order creation through commands
- Background processing simulation
- Message flow through MassTransit
- Database state transitions
- Health check validation

## 🧪 Testing Strategy

### Test Hierarchy

The application includes comprehensive testing at multiple levels:

#### Unit Tests
```bash
# Run domain logic tests
dotnet test tests/WorkerService.UnitTests/ --logger "console;verbosity=normal"
```

**Focus Areas:**
- Domain entity behavior and business rules
- Value object validation (Money, OrderStatus)
- Domain event generation and handling
- Business logic invariants

#### Integration Tests
```bash
# Run all integration tests
dotnet test tests/WorkerService.IntegrationTests/ --logger "console;verbosity=normal"

# Run specific test categories
dotnet test --filter "InMemoryConfigurationTests"
dotnet test --filter "BackgroundServiceIntegrationTests" 
dotnet test --filter "MessageFlowIntegrationTests"
```

**Test Categories:**

| Test Suite | Purpose | Features Tested |
|------------|---------|-----------------|
| `InMemoryConfigurationTests` | Validates in-memory provider functionality | Configuration parsing, conditional service registration |
| `BackgroundServiceIntegrationTests` | Tests worker service behavior | Order processing, cancellation, error handling |
| `HealthCheckIntegrationTests` | Validates monitoring capabilities | Dependency health, response times, status reporting |
| `MessageFlowIntegrationTests` | Tests event-driven communication | MassTransit consumers, message correlation, retry policies |
| `OrderProcessingIntegrationTests` | End-to-end workflow validation | Complete order lifecycle, state transitions |

#### Test Configuration

Tests automatically use in-memory providers for fast execution:

```csharp
// Integration tests are pre-configured for in-memory operation
services.Configure<InMemorySettings>(config => 
{
    config.UseDatabase = true;      // In-memory EF Core
    config.UseMessageBroker = true; // In-memory MassTransit
});
```

## 🔍 Monitoring & Observability

### Health Check Endpoints

When running in Development mode or with `HealthChecks:Enabled=true`:

```bash
# Check overall application health
curl http://localhost:5000/health

# Check readiness (dependencies available)
curl http://localhost:5000/health/ready

# Check liveness (application responsive)
curl http://localhost:5000/health/live
```

### OpenTelemetry Integration

The application exports telemetry data compatible with:

- **Jaeger**: Distributed tracing (http://localhost:16686)
- **Prometheus**: Metrics collection (http://localhost:9090)  
- **Grafana**: Visualization dashboards (http://localhost:3000)

**Key Metrics Tracked:**
- Order processing rates and timing
- Background service execution frequency
- Database query performance
- Message processing throughput
- Health check response times

### Structured Logging

All logs include contextual information:

```json
{
  "timestamp": "2024-01-15T10:30:00.123Z",
  "level": "Information", 
  "message": "OrderProcessingService starting",
  "properties": {
    "ServiceName": "WorkerService",
    "Environment": "Development",
    "Configuration": "Database: In-Memory, MessageBroker: In-Memory",
    "CorrelationId": "abc123-def456"
  }
}
```

## 🚀 Production Deployment

### Environment Configuration

For production deployment, ensure:

```bash
# Required environment variables
DOTNET_ENVIRONMENT=Production
InMemory__UseDatabase=false
InMemory__UseMessageBroker=false

# Connection strings
ConnectionStrings__DefaultConnection="Host=prod-db;Database=orders;Username=app;Password=***"
ConnectionStrings__RabbitMQ="amqp://user:pass@prod-rabbitmq:5672/"

# Observability
OTEL_EXPORTER_OTLP_ENDPOINT=https://your-collector:4317
OTEL_SERVICE_NAME=OrderProcessingService
OTEL_RESOURCE_ATTRIBUTES=environment=production,version=1.0.0
```

### Docker Deployment

```dockerfile
# Production Dockerfile example
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["src/WorkerService.Worker/WorkerService.Worker.csproj", "src/WorkerService.Worker/"]
RUN dotnet restore "src/WorkerService.Worker/WorkerService.Worker.csproj"
COPY . .
WORKDIR "/src/src/WorkerService.Worker"
RUN dotnet build "WorkerService.Worker.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "WorkerService.Worker.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "WorkerService.Worker.dll"]
```

## 📚 Additional Resources

- **[Clean Architecture Template](https://github.com/jasontaylordev/CleanArchitecture)**: Reference implementation
- **[MassTransit Documentation](https://masstransit.io/)**: Advanced messaging patterns
- **[EF Core Performance](https://learn.microsoft.com/en-us/ef/core/performance/)**: Optimization techniques
- **[OpenTelemetry .NET](https://opentelemetry.io/docs/languages/dotnet/)**: Observability best practices
- **[.NET Worker Services](https://learn.microsoft.com/en-us/dotnet/core/extensions/workers)**: Background service patterns

---

## 🎯 Quick Commands Reference

```bash
# Development (in-memory, fast startup)
dotnet run --project src/WorkerService.Worker/ --environment Development

# Production-like (with Docker)
docker-compose up -d && dotnet run --project src/WorkerService.Worker/

# Run all tests
dotnet test

# Build for deployment
dotnet publish src/WorkerService.Worker/ -c Release -o ./publish

# Check health
curl http://localhost:5000/health
```

**Happy coding! 🚀**
