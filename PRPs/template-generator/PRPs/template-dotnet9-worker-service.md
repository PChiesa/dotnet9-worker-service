---
name: ".NET 9 Worker Service Template Generator"
description: "Generate a comprehensive context engineering template for building production-ready .NET 9 Worker Services with Clean Architecture, MassTransit, PostgreSQL, and OpenTelemetry"
---

## Purpose

Generate a complete context engineering template package for .NET 9 Worker Services optimized for building resilient, observable, and maintainable background processing services following Clean Architecture principles with integrated message brokering, data persistence, and comprehensive observability.

## Core Principles

1. **Clean Architecture Enforcement**: Strict separation of concerns with Domain → Application → Infrastructure dependency flow
2. **Production-Ready Patterns**: Enterprise-grade patterns including health checks, graceful shutdown, and comprehensive observability
3. **Message-Driven Architecture**: MassTransit integration for robust message handling with saga support
4. **Database Best Practices**: EF Core with PostgreSQL using repository pattern and proper DbContext lifecycle management
5. **Observability First**: OpenTelemetry integration capturing traces, metrics, and logs across all components

---

## Goal

Generate a complete context engineering template package for **.NET 9 Worker Service** that includes:

- Clean Architecture solution structure with proper layer separation
- MassTransit configuration for RabbitMQ with consumer patterns and saga support
- PostgreSQL integration using EF Core 9 with repository pattern
- OpenTelemetry observability with automatic instrumentation
- Health check endpoints via minimal API
- Complete example workflows and integration tests
- Domain-specific validation loops and deployment patterns

## Why

- **Architecture Consistency**: Enforce Clean Architecture principles across all worker services
- **Message Processing Excellence**: Leverage MassTransit's powerful features for reliable messaging
- **Production Readiness**: Include all necessary patterns for real-world deployment
- **Observability Built-In**: Ensure comprehensive monitoring and debugging capabilities
- **Developer Productivity**: Provide immediate starting point with working examples

## What

### Template Package Components

**Complete Directory Structure:**
```
use-cases/dotnet9-worker-service/
├── CLAUDE.md                              # .NET Worker Service implementation guide
├── .claude/commands/
│   ├── generate-worker-prp.md             # Worker Service PRP generation
│   └── execute-worker-prp.md              # Worker Service PRP execution  
├── PRPs/
│   ├── templates/
│   │   └── prp_worker_base.md             # Worker Service base PRP template
│   └── INITIAL.md                         # Example feature request
├── examples/
│   ├── src/
│   │   ├── WorkerService.Domain/          # Domain entities and interfaces
│   │   ├── WorkerService.Application/     # Business logic and use cases
│   │   ├── WorkerService.Infrastructure/  # Data access and external services
│   │   └── WorkerService.Worker/          # Worker host and consumers
│   ├── tests/
│   │   ├── WorkerService.UnitTests/       # Unit test examples
│   │   └── WorkerService.IntegrationTests/# Integration test examples
│   ├── WorkerService.sln                  # Solution file
│   └── docker-compose.yml                 # Local development infrastructure
├── copy_template.py                        # Template deployment script
└── README.md                              # Comprehensive usage guide
```

### Technology Stack Details

**Based on extensive web research findings:**

1. **.NET 9 Generic Host & BackgroundService**
   - Latest patterns for long-running services
   - Graceful shutdown handling with CancellationToken
   - Proper lifecycle management

2. **MassTransit Integration**
   - RabbitMQ transport with quorum queues
   - Consumer patterns with dependency injection
   - Saga state machines for complex workflows
   - Built-in OpenTelemetry support

3. **Clean Architecture Implementation**
   - Jason Taylor's template patterns adapted for Worker Services
   - CQRS with MediatR for command/query separation
   - Repository pattern for data access abstraction

4. **PostgreSQL with EF Core 9**
   - Npgsql.EntityFrameworkCore.PostgreSQL 9.0.4
   - DbContext lifetime management for background services
   - Migration strategies and connection pooling

5. **OpenTelemetry Observability**
   - Comprehensive tracing across MassTransit and EF Core
   - Metrics collection with Prometheus export
   - Structured logging with OTLP export
   - Environment variable configuration

6. **Health Checks**
   - Minimal API endpoints for health monitoring
   - RabbitMQ and PostgreSQL health check providers
   - Worker service status reporting

### Success Criteria

- [ ] Complete Clean Architecture solution structure generated
- [ ] MassTransit configured with working consumer examples
- [ ] PostgreSQL integration with proper DbContext management
- [ ] OpenTelemetry fully configured with all instrumentations
- [ ] Health check endpoints accessible and functional
- [ ] Integration tests using MassTransit test harness
- [ ] Docker Compose for local development environment
- [ ] All examples compile and run successfully

## All Needed Context

### Documentation & References (From Web Research)

```yaml
# .NET 9 WORKER SERVICE DOCUMENTATION
- url: https://learn.microsoft.com/en-us/dotnet/core/extensions/workers
  why: Core Worker Service patterns and BackgroundService implementation

- url: https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host
  why: Generic Host configuration and lifetime management

- url: https://learn.microsoft.com/en-us/dotnet/core/extensions/scoped-service
  why: Scoped service usage in background tasks - critical for DbContext

# MASSTRANSIT INTEGRATION
- url: https://masstransit.io/documentation/configuration/transports/rabbitmq
  why: RabbitMQ configuration with quorum queues and best practices

- url: https://masstransit.io/documentation/configuration/sagas/custom
  why: Saga state machine patterns for complex workflows

- url: https://masstransit.io/documentation/configuration/observability
  why: Built-in OpenTelemetry support and metrics

# CLEAN ARCHITECTURE
- url: https://github.com/jasontaylordev/CleanArchitecture
  why: Jason Taylor's Clean Architecture template - reference implementation

- url: https://jasontaylor.dev/clean-architecture-getting-started/
  why: Clean Architecture principles and layer separation

# POSTGRESQL & EF CORE
- url: https://www.npgsql.org/efcore/
  why: Npgsql Entity Framework Core Provider documentation

- url: https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/
  why: DbContext lifetime management - critical for Worker Services

# OPENTELEMETRY
- url: https://opentelemetry.io/docs/languages/dotnet/getting-started/
  why: OpenTelemetry .NET SDK configuration

- url: https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md
  why: OTLP exporter configuration and environment variables

# HEALTH CHECKS
- url: https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-9.0
  why: ASP.NET Core Health Checks implementation

- url: https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks
  why: RabbitMQ and PostgreSQL health check providers
```

### Research Findings Summary

```typescript
// Key patterns discovered through web research
const workerServicePatterns = {
  // BackgroundService implementation
  lifecycle: {
    startup: "IHostedService.StartAsync",
    execution: "BackgroundService.ExecuteAsync",
    shutdown: "Graceful shutdown with CancellationToken",
    error_handling: "BackgroundServiceExceptionBehavior.StopHost"
  },
  
  // MassTransit configuration
  messaging: {
    transport: "RabbitMQ with quorum queues",
    consumers: "IConsumer<T> with DI support",
    sagas: "State machines with persistence",
    observability: "Built-in OpenTelemetry support"
  },
  
  // DbContext management
  data_access: {
    lifetime: "Scoped via IServiceScopeFactory",
    alternatives: "IDbContextFactory for on-demand creation",
    pooling: "AddPooledDbContextFactory for performance",
    gotcha: "Never share DbContext across threads"
  },
  
  // Observability configuration
  telemetry: {
    tracing: "AddSource('MassTransit') for message tracing",
    metrics: "AddMeter('MassTransit') for metrics",
    logging: "Structured logging with OTLP export",
    configuration: "Environment variables for production"
  }
};
```

### Known Implementation Patterns

```yaml
CRITICAL_PATTERNS:
  DbContext_Scoping:
    - Always create scope in BackgroundService.ExecuteAsync
    - Use IServiceScopeFactory.CreateScope()
    - Dispose scope after each unit of work
    - Never inject DbContext directly into singleton services
  
  MassTransit_Configuration:
    - Configure prefetch count to prevent memory issues
    - Use quorum queues for reliability (replication factor 3)
    - Enable batch publishing for performance
    - Implement idempotent consumers
  
  Health_Check_Implementation:
    - Target Microsoft.NET.Sdk.Web for minimal API support
    - Register IConnection as singleton for RabbitMQ checks
    - Use /health endpoint for basic status
    - Implement custom WorkerHealthCheck for service status
  
  OpenTelemetry_Setup:
    - Configure all instrumentations (AspNetCore, Http, EF, SqlClient, MassTransit)
    - Use OTLP exporter with environment variable configuration
    - Enable metrics endpoint for Prometheus scraping
    - Include service metadata in resource attributes
```

## Implementation Blueprint

### Phase 1: Template Structure Generation

```yaml
Task 1 - Create Clean Architecture Solution:
  CREATE solution structure following Jason Taylor patterns:
    - Generate .sln file with proper project references
    - Create Domain project (no dependencies)
    - Create Application project (depends on Domain)
    - Create Infrastructure project (depends on Application)
    - Create Worker project (depends on Infrastructure, Application)
    - Add test projects for unit and integration testing
    
Task 2 - Configure Project Dependencies:
  ADD NuGet packages based on research:
    Domain:
      - No external dependencies (POCO only)
    
    Application:
      - MediatR (for CQRS)
      - FluentValidation
      - Microsoft.Extensions.Logging.Abstractions
    
    Infrastructure:
      - Npgsql.EntityFrameworkCore.PostgreSQL 9.0.4
      - MassTransit.RabbitMQ
      - MassTransit.EntityFrameworkCore
    
    Worker:
      - Microsoft.Extensions.Hosting
      - OpenTelemetry.Extensions.Hosting
      - OpenTelemetry.Instrumentation.* (all required)
      - AspNetCore.HealthChecks.Rabbitmq
      - AspNetCore.HealthChecks.NpgSql
```

### Phase 2: Core Implementation

```yaml
Task 3 - Implement Domain Layer:
  CREATE domain entities and interfaces:
    - Order entity with business logic
    - IRepository<T> interface
    - Domain events for message publishing
    - Value objects for type safety
    
Task 4 - Implement Application Layer:
  CREATE CQRS handlers and business logic:
    - CreateOrderCommand with MediatR handler
    - OrderCreatedEvent for MassTransit
    - Validation with FluentValidation
    - Application service interfaces
    
Task 5 - Implement Infrastructure Layer:
  CREATE data access and messaging:
    - ApplicationDbContext with PostgreSQL configuration
    - Repository<T> implementation
    - MassTransit consumer implementations
    - External service integrations
```

### Phase 3: Worker Service Configuration

```yaml
Task 6 - Configure Worker Host:
  IMPLEMENT Program.cs with all integrations:
    - Generic Host configuration
    - MassTransit with RabbitMQ setup
    - EF Core with PostgreSQL
    - OpenTelemetry configuration
    - Health checks with minimal API
    - Proper service registration
    
Task 7 - Implement Background Services:
  CREATE worker implementations:
    - OrderProcessingService : BackgroundService
    - Proper scope management for DbContext
    - Graceful shutdown handling
    - Error handling and retry logic
```

### Phase 4: Observability and Health

```yaml
Task 8 - Configure OpenTelemetry:
  SETUP comprehensive observability:
    - Tracing for all components
    - Metrics with Prometheus export
    - Structured logging with OTLP
    - Environment variable configuration
    - Service metadata attributes
    
Task 9 - Implement Health Checks:
  CREATE health monitoring:
    - RabbitMQ connectivity check
    - PostgreSQL database check
    - Custom worker status check
    - /health endpoint configuration
    - Health check UI response
```

### Phase 5: Testing and Examples

```yaml
Task 10 - Create Integration Tests:
  IMPLEMENT comprehensive tests:
    - MassTransit test harness setup
    - In-memory database for testing
    - Consumer behavior validation
    - End-to-end workflow tests
    
Task 11 - Add Docker Compose:
  CREATE local development environment:
    - RabbitMQ container with management UI
    - PostgreSQL container with initialization
    - Jaeger for trace visualization
    - Prometheus for metrics
    - Environment variable configuration
```

### Phase 6: Documentation and Templates

```yaml
Task 12 - Generate CLAUDE.md:
  CREATE domain-specific rules:
    - Clean Architecture enforcement rules
    - MassTransit consumer patterns
    - DbContext scoping requirements
    - Testing standards
    - Deployment considerations
    
Task 13 - Create Command Templates:
  GENERATE specialized commands:
    - generate-worker-prp.md with research prompts
    - execute-worker-prp.md with validation loops
    - Technology-specific patterns
    
Task 14 - Develop Examples:
  CREATE working examples:
    - Complete order processing workflow
    - Health check implementation
    - Integration test examples
    - Configuration samples
    
Task 15 - Write Comprehensive README:
  DOCUMENT everything:
    - Quick start with copy script
    - Architecture overview
    - Configuration guide
    - Common patterns
    - Troubleshooting
```

## Validation Loop

### Level 1: Solution Structure Validation

```bash
# Verify Clean Architecture structure
ls -la examples/src/ | grep -E "Domain|Application|Infrastructure|Worker"
dotnet sln examples/WorkerService.sln list

# Check project references follow dependency rules
dotnet list examples/src/WorkerService.Domain/ reference  # Should be empty
dotnet list examples/src/WorkerService.Application/ reference  # Only Domain
```

### Level 2: Build and Compilation

```bash
# Build entire solution
cd examples/
dotnet restore
dotnet build --no-restore

# Run unit tests
dotnet test tests/WorkerService.UnitTests/ --no-build
```

### Level 3: Integration Testing

```bash
# Start dependencies
docker-compose up -d rabbitmq postgres

# Run integration tests with test harness
dotnet test tests/WorkerService.IntegrationTests/ --no-build

# Verify health checks
curl http://localhost:5000/health
```

### Level 4: Observability Validation

```bash
# Check OpenTelemetry configuration
grep -r "AddOpenTelemetry" src/WorkerService.Worker/
grep -r "OTEL_" docker-compose.yml

# Verify all instrumentations present
grep -E "AddAspNetCoreInstrumentation|AddEntityFrameworkCoreInstrumentation|AddSource.*MassTransit" src/WorkerService.Worker/Program.cs
```

## Final Validation Checklist

### Architecture and Code Quality

- [ ] Clean Architecture layers properly separated
- [ ] No circular dependencies between layers
- [ ] Repository pattern correctly implemented
- [ ] CQRS pattern with MediatR working
- [ ] All services properly registered in DI

### Messaging and Data

- [ ] MassTransit consumers processing messages
- [ ] RabbitMQ connection resilient with retry
- [ ] PostgreSQL migrations working
- [ ] DbContext scoping correct in background services
- [ ] Saga state machines if implemented

### Observability and Operations

- [ ] OpenTelemetry exporting traces, metrics, logs
- [ ] Health checks returning accurate status
- [ ] Graceful shutdown working properly
- [ ] Docker Compose starts all services
- [ ] Integration tests passing

### Developer Experience

- [ ] Copy script deploys template successfully
- [ ] README provides clear quick start
- [ ] Examples compile and run
- [ ] Common gotchas documented
- [ ] PRP workflow functional

---

## Anti-Patterns to Avoid

### Architecture Violations

- ❌ Don't reference Infrastructure from Domain
- ❌ Don't put business logic in Infrastructure
- ❌ Don't bypass Application layer from Worker
- ❌ Don't create circular dependencies

### Common Mistakes

- ❌ Don't inject DbContext into singletons
- ❌ Don't share DbContext across threads
- ❌ Don't forget to dispose scopes
- ❌ Don't ignore cancellation tokens
- ❌ Don't skip idempotency in consumers

### Observability Pitfalls

- ❌ Don't hardcode OTLP endpoints
- ❌ Don't skip security headers
- ❌ Don't forget service metadata
- ❌ Don't ignore performance impact

**Confidence Score: 9/10**

This PRP provides a comprehensive blueprint for creating a production-ready .NET 9 Worker Service template with all requested technologies, based on extensive research of official documentation and best practices.