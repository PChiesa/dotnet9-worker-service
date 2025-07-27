---
name: "Worker Service Feature PRP Base"
description: "Base template for generating .NET 9 Worker Service features with Clean Architecture, MassTransit, PostgreSQL, and OpenTelemetry"
---

## Purpose

Implement a production-ready .NET 9 Worker Service feature following Clean Architecture principles with comprehensive messaging, data persistence, and observability capabilities.

## Core Principles

1. **Clean Architecture Compliance**: Strict layer separation with Domain → Application → Infrastructure → Worker dependency flow
2. **Message-Driven Design**: MassTransit integration for reliable, scalable message processing
3. **Data Consistency**: PostgreSQL with EF Core using proper DbContext lifetime management
4. **Observability First**: OpenTelemetry instrumentation across all components
5. **Production Readiness**: Health checks, graceful shutdown, and comprehensive error handling

---

## Goal

Implement **[FEATURE_NAME]** as a .NET 9 Worker Service feature that includes:

- Clean Architecture layer implementation with proper dependencies
- MassTransit consumer/producer patterns for message handling
- PostgreSQL integration with EF Core and repository pattern
- OpenTelemetry tracing, metrics, and logging
- Health checks and operational monitoring
- Comprehensive unit and integration testing

## Why

- **Scalability**: Message-driven architecture enables horizontal scaling
- **Reliability**: MassTransit provides retry policies, error handling, and message durability
- **Maintainability**: Clean Architecture ensures testable, modular code
- **Observability**: OpenTelemetry provides comprehensive monitoring and debugging
- **Production Readiness**: Health checks and graceful shutdown ensure operational excellence

## What

### Implementation Components

**Domain Layer (No Dependencies)**:
- Entities with business logic encapsulation
- Value objects for type safety
- Repository interfaces
- Domain events for messaging

**Application Layer (Depends on Domain Only)**:
- CQRS command/query handlers using MediatR
- Business logic services
- FluentValidation validators
- Application interfaces

**Infrastructure Layer (Depends on Application & Domain)**:
- ApplicationDbContext with PostgreSQL configuration
- Repository implementations
- MassTransit consumer implementations
- External service integrations

**Worker Layer (Depends on Infrastructure & Application)**:
- BackgroundService implementations
- Program.cs with comprehensive configuration
- Health check implementations
- Message publishers and workflow orchestration

### Success Criteria

- [ ] Clean Architecture layers properly separated with correct dependencies
- [ ] MassTransit consumers processing messages reliably
- [ ] PostgreSQL integration with proper DbContext scoping
- [ ] OpenTelemetry capturing traces, metrics, and logs
- [ ] Health checks reporting accurate dependency status
- [ ] Unit tests covering domain logic and application handlers
- [ ] Integration tests validating end-to-end workflows
- [ ] Background services implementing graceful shutdown

## All Needed Context

### Documentation & References

```yaml
# .NET 9 WORKER SERVICE PATTERNS
- url: https://learn.microsoft.com/en-us/dotnet/core/extensions/workers
  why: BackgroundService implementation patterns and lifecycle management

- url: https://learn.microsoft.com/en-us/dotnet/core/extensions/scoped-service
  why: Critical for DbContext lifetime management in background services

- url: https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host
  why: Generic Host configuration and service registration

# MASSTRANSIT INTEGRATION
- url: https://masstransit.io/documentation/configuration/transports/rabbitmq
  why: RabbitMQ transport configuration and quorum queue setup

- url: https://masstransit.io/documentation/configuration/consumers
  why: Consumer implementation patterns and dependency injection

- url: https://masstransit.io/documentation/configuration/sagas
  why: Saga state machine patterns for complex workflows

# CLEAN ARCHITECTURE
- url: https://github.com/jasontaylordev/CleanArchitecture
  why: Reference implementation of Clean Architecture in .NET

- url: https://jasontaylor.dev/clean-architecture-getting-started/
  why: Clean Architecture principles and layer dependencies

# EF CORE & POSTGRESQL
- url: https://www.npgsql.org/efcore/
  why: Npgsql Entity Framework Core Provider documentation

- url: https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/
  why: DbContext lifetime and scoping strategies

# OPENTELEMETRY
- url: https://opentelemetry.io/docs/languages/dotnet/getting-started/
  why: OpenTelemetry .NET SDK configuration and instrumentation

- url: https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md
  why: OTLP exporter configuration and environment variables

# HEALTH CHECKS
- url: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-9.0
  why: Health check implementation patterns

- url: https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks
  why: RabbitMQ and PostgreSQL health check providers
```

### Current Architecture Context

```csharp
// Required project structure for Clean Architecture compliance
src/
├── WorkerService.Domain/          // No external dependencies
├── WorkerService.Application/     // Depends on Domain only
├── WorkerService.Infrastructure/  // Depends on Application & Domain
└── WorkerService.Worker/          // Depends on Infrastructure & Application

// Required NuGet packages by layer
Domain: No external packages (POCO only)
Application: MediatR, FluentValidation, Microsoft.Extensions.Logging.Abstractions
Infrastructure: Npgsql.EntityFrameworkCore.PostgreSQL, MassTransit.RabbitMQ, MassTransit.EntityFrameworkCore
Worker: Microsoft.Extensions.Hosting, OpenTelemetry.*, AspNetCore.HealthChecks.*
```

### Technology Configuration Patterns

```csharp
// MassTransit configuration pattern
services.AddMassTransit(x =>
{
    x.AddConsumer<YourConsumer>();
    
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq://localhost");
        cfg.ReceiveEndpoint("your-queue", e =>
        {
            e.SetQuorumQueue(3); // For reliability
            e.ConfigureConsumer<YourConsumer>(context);
        });
    });
});

// EF Core with proper DbContext scoping in BackgroundService
public class YourBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            // Work with scoped services
        }
    }
}

// OpenTelemetry comprehensive configuration
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("WorkerService"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddSqlClientInstrumentation()
        .AddSource("MassTransit")
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddMeter("MassTransit")
        .AddOtlpExporter());
```

### Known Implementation Patterns

```typescript
// Critical patterns for Worker Service implementation
const workerServicePatterns = {
  // Layer separation (CRITICAL)
  dependencies: {
    domain: [], // No external dependencies
    application: ["Domain"], // Only Domain
    infrastructure: ["Application", "Domain"], // Application and Domain
    worker: ["Infrastructure", "Application"] // Infrastructure and Application
  },
  
  // DbContext lifetime management (CRITICAL)
  dataAccess: {
    pattern: "IServiceScopeFactory in BackgroundService",
    antipattern: "Direct DbContext injection into singleton",
    scope: "Create and dispose per unit of work",
    threading: "Never share DbContext across threads"
  },
  
  // MassTransit configuration
  messaging: {
    consumers: "IConsumer<T> with proper DI registration",
    reliability: "Quorum queues with replication factor 3",
    observability: "AddSource('MassTransit') for tracing",
    errorHandling: "Retry policies and circuit breaker patterns"
  },
  
  // Background service patterns
  lifecycle: {
    startup: "Implement StartAsync for initialization",
    execution: "ExecuteAsync with CancellationToken support",
    shutdown: "Graceful shutdown with proper cleanup",
    scoping: "Use IServiceScopeFactory for scoped services"
  }
};
```

## Implementation Blueprint

### Phase 1: Domain Layer Implementation

```yaml
Task 1 - Define Domain Entities:
  CREATE business entities with encapsulated logic:
    - Entity base class with common properties
    - Business entities with domain logic
    - Value objects for type safety
    - Domain events for messaging integration
    
Task 2 - Define Repository Interfaces:
  CREATE abstractions for data access:
    - IRepository<T> generic interface
    - Specific repository interfaces for entities
    - Unit of work interface if needed
    - Query interfaces for complex operations
```

### Phase 2: Application Layer Implementation

```yaml
Task 3 - Implement CQRS Handlers:
  CREATE command and query handlers:
    - Command DTOs and handlers for state changes
    - Query DTOs and handlers for data retrieval
    - MediatR pipeline behaviors for cross-cutting concerns
    - FluentValidation validators for input validation
    
Task 4 - Define Application Services:
  CREATE business logic services:
    - Application service interfaces
    - Domain event handlers
    - Integration event interfaces
    - Business workflow orchestration
```

### Phase 3: Infrastructure Layer Implementation

```yaml
Task 5 - Configure Data Access:
  IMPLEMENT EF Core with PostgreSQL:
    - ApplicationDbContext with entity configurations
    - Repository implementations
    - Migration setup and seeding
    - Connection string configuration
    
Task 6 - Implement MassTransit Consumers:
  CREATE message consumers:
    - Consumer implementations for each message type
    - Saga state machines for complex workflows
    - Error handling and retry policies
    - Message routing configuration
```

### Phase 4: Worker Layer Implementation

```yaml
Task 7 - Configure Program.cs:
  SETUP comprehensive service registration:
    - Generic Host configuration
    - MassTransit with RabbitMQ transport
    - EF Core with PostgreSQL
    - OpenTelemetry with all instrumentations
    - Health checks for all dependencies
    
Task 8 - Implement Background Services:
  CREATE BackgroundService implementations:
    - Proper IServiceScopeFactory usage
    - CancellationToken support throughout
    - Error handling and logging
    - Graceful shutdown implementation
```

### Phase 5: Testing Implementation

```yaml
Task 9 - Create Unit Tests:
  IMPLEMENT comprehensive unit tests:
    - Domain entity tests
    - Application handler tests
    - Repository interface tests
    - Validation rule tests
    
Task 10 - Create Integration Tests:
  IMPLEMENT end-to-end tests:
    - MassTransit test harness setup
    - In-memory database testing
    - Consumer behavior validation
    - Health check testing
```

## Validation Loop

### Level 1: Architecture Validation

```bash
# Verify Clean Architecture structure
find src/ -name "*.csproj" | sort
dotnet sln list

# Check dependency rules (CRITICAL)
dotnet list src/WorkerService.Domain/ reference | wc -l  # Should be 0
dotnet list src/WorkerService.Application/ reference | grep -c Domain  # Should be 1
dotnet list src/WorkerService.Infrastructure/ reference | grep -c -E "Application|Domain"  # Should be 2
```

### Level 2: Build and Package Validation

```bash
# Build verification
dotnet restore
dotnet build --no-restore

# Package verification
dotnet list package | grep -E "MassTransit|Npgsql|OpenTelemetry|HealthChecks"
```

### Level 3: Technology Integration Validation

```bash
# MassTransit configuration
grep -r "AddMassTransit" src/WorkerService.Worker/
grep -r "IConsumer" src/WorkerService.Infrastructure/

# EF Core DbContext scoping (CRITICAL)
grep -r "IServiceScopeFactory" src/WorkerService.Worker/
grep -r "UseNpgsql" src/WorkerService.Infrastructure/

# OpenTelemetry instrumentation
grep -r "AddOpenTelemetry" src/WorkerService.Worker/
grep -E "AddSource.*MassTransit" src/WorkerService.Worker/
```

### Level 4: Runtime Validation

```bash
# Docker environment
docker-compose up -d
curl http://localhost:5000/health

# Application startup
dotnet run --project src/WorkerService.Worker/ &
sleep 10 && curl http://localhost:5000/health
```

## Final Validation Checklist

### Architecture Compliance

- [ ] Domain layer has no external dependencies
- [ ] Application layer depends only on Domain
- [ ] Infrastructure layer depends on Application and Domain
- [ ] Worker layer depends on Infrastructure and Application
- [ ] No circular dependencies between projects

### Technology Integration

- [ ] MassTransit consumers properly registered and configured
- [ ] EF Core DbContext lifetime properly managed in background services
- [ ] OpenTelemetry instrumentation configured for all components
- [ ] Health checks implemented for RabbitMQ, PostgreSQL, and worker status
- [ ] Docker Compose environment functional

### Code Quality

- [ ] Repository pattern correctly implemented
- [ ] CQRS pattern with MediatR working
- [ ] Background services implement proper cancellation
- [ ] Unit and integration tests comprehensive and passing
- [ ] Error handling and logging implemented throughout

---

## Anti-Patterns to Avoid

### Architecture Violations

- ❌ Don't reference Infrastructure from Domain or Application
- ❌ Don't put business logic in Infrastructure layer
- ❌ Don't create circular dependencies between projects
- ❌ Don't bypass Clean Architecture layer boundaries

### DbContext Lifetime Issues

- ❌ Don't inject DbContext directly into singleton services
- ❌ Don't share DbContext instances across threads
- ❌ Don't forget to dispose scopes in background services
- ❌ Don't use DbContext outside of proper scope

### MassTransit Pitfalls

- ❌ Don't configure unlimited prefetch counts
- ❌ Don't ignore idempotency in consumer implementations
- ❌ Don't skip error handling and retry policies
- ❌ Don't forget correlation ID configuration

### Worker Service Mistakes

- ❌ Don't ignore cancellation tokens in background operations
- ❌ Don't block async operations with .Result
- ❌ Don't skip graceful shutdown implementation
- ❌ Don't forget comprehensive error handling and logging

**Confidence Score: [SCORE]/10**

[Brief explanation of confidence level based on research depth and implementation complexity]