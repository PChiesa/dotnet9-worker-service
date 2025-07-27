# Generate Worker Service PRP

## Feature file: {filename}

Generate a comprehensive PRP for .NET 9 Worker Service features based on Clean Architecture principles with MassTransit, PostgreSQL, and OpenTelemetry integration.

**CRITICAL: Web search and documentation research is your best friend. Use it extensively throughout this process.**

## Research Process

1. **Read and Understand Requirements**
   - Read the specified INITIAL.md file thoroughly
   - Understand the specific Worker Service feature requirements
   - Note any messaging patterns, data persistence needs, or observability requirements
   - Identify integration points with Clean Architecture layers

2. **Extensive Web Research (CRITICAL)**
   - **Web search .NET 9 Worker Service patterns** - research latest BackgroundService implementations
   - **Study MassTransit documentation** - consumer patterns, saga implementations, retry policies
   - **Research Clean Architecture patterns** - CQRS, repository patterns, dependency injection
   - **Investigate EF Core 9 with PostgreSQL** - DbContext lifetime, migrations, performance patterns
   - **Study OpenTelemetry .NET SDK** - instrumentation, exporters, correlation patterns
   - **Research health check implementations** - custom checks, dependency monitoring

3. **Technology Pattern Analysis**
   - Examine Clean Architecture layer separation and dependency flow
   - Identify CQRS command/query patterns with MediatR
   - Extract MassTransit consumer and saga state machine patterns
   - Document EF Core DbContext scoping strategies for background services
   - Note OpenTelemetry configuration and instrumentation requirements

4. **Worker Service Adaptation**
   - Map discovered patterns to Worker Service architecture
   - Plan BackgroundService implementations with proper scoping
   - Design message-driven workflows with MassTransit
   - Plan database interaction patterns with proper lifetime management

## PRP Generation

Using PRPs/templates/prp_worker_base.md as the foundation:

### Critical Context to Include from Web Research

**Technology Documentation (from web search)**:
- .NET 9 Worker Service and Generic Host documentation
- MassTransit configuration guides and consumer patterns
- Clean Architecture implementation examples (Jason Taylor's template)
- EF Core 9 with PostgreSQL integration guides
- OpenTelemetry .NET SDK instrumentation documentation

**Implementation Patterns (from research)**:
- BackgroundService lifecycle management and graceful shutdown
- MassTransit consumer registration and dependency injection
- DbContext scoping with IServiceScopeFactory in background services
- Repository pattern implementation across Clean Architecture layers
- CQRS handler patterns with MediatR

**Real-World Examples**:
- Working Consumer implementations with proper error handling
- DbContext usage patterns in BackgroundService implementations
- OpenTelemetry configuration with all required instrumentations
- Health check implementations for RabbitMQ and PostgreSQL
- Integration test patterns with MassTransit TestFramework

### Implementation Blueprint

Based on web research findings:
- **Clean Architecture Analysis**: Layer separation, dependency rules, project structure
- **Messaging Strategy**: Consumer patterns, saga implementations, retry and error handling
- **Data Access Design**: Repository patterns, DbContext lifetime, migration strategies
- **Observability Planning**: Tracing, metrics, logging configuration across all components

### Validation Gates (Must be Executable)

```bash
# Solution Structure Validation
dotnet sln list | grep -E "Domain|Application|Infrastructure|Worker"
dotnet list src/WorkerService.Domain/ reference  # Should be empty
dotnet list src/WorkerService.Application/ reference  # Only Domain

# Build and Test Validation
dotnet restore
dotnet build --no-restore
dotnet test --no-build

# MassTransit Configuration Validation
grep -r "AddMassTransit" src/WorkerService.Worker/
grep -r "IConsumer" src/WorkerService.Infrastructure/

# EF Core Configuration Validation
grep -r "IServiceScopeFactory" src/WorkerService.Worker/
grep -r "UseNpgsql" src/WorkerService.Infrastructure/

# OpenTelemetry Configuration Validation
grep -r "AddOpenTelemetry" src/WorkerService.Worker/
grep -E "AddAspNetCoreInstrumentation|AddEntityFrameworkCoreInstrumentation" src/WorkerService.Worker/

# Health Check Validation
curl http://localhost:5000/health
grep -r "AddHealthChecks" src/WorkerService.Worker/

# Docker Environment Validation
docker-compose up -d
docker-compose ps | grep -E "rabbitmq|postgres"
```

*** CRITICAL: Do extensive web research before writing the PRP ***
*** Use WebSearch tool extensively to understand latest .NET 9 and MassTransit patterns ***

## Worker Service Specific Considerations

### BackgroundService Patterns
- **Lifecycle Management**: StartAsync, ExecuteAsync, StopAsync implementations
- **Cancellation Token Usage**: Proper cancellation handling throughout the service
- **Scope Creation**: IServiceScopeFactory usage for DbContext and other scoped services
- **Error Handling**: Exception policies and retry patterns

### MassTransit Integration
- **Consumer Registration**: Proper DI registration with AddConsumer<T>
- **Transport Configuration**: RabbitMQ setup with quorum queues
- **Message Routing**: Exchange and queue configuration
- **Saga Implementation**: State machine patterns for complex workflows

### Clean Architecture Compliance
- **Domain Layer**: Entities, value objects, domain events (no dependencies)
- **Application Layer**: CQRS handlers, services, interfaces (depends only on Domain)
- **Infrastructure Layer**: Repositories, DbContext, consumers (depends on Application)
- **Worker Layer**: BackgroundServices, Program.cs (depends on Infrastructure and Application)

### Database Integration
- **DbContext Lifetime**: Scoped service creation in BackgroundService
- **Repository Pattern**: Interface in Domain, implementation in Infrastructure
- **Migration Strategy**: Code-first migrations with proper seeding
- **Connection Management**: Connection string configuration and pooling

### Observability Requirements
- **Distributed Tracing**: OpenTelemetry configuration with correlation IDs
- **Metrics Collection**: Business and infrastructure metrics
- **Structured Logging**: Correlation IDs and contextual information
- **Health Monitoring**: Custom health checks for all dependencies

## Output

Save as: `PRPs/{feature-name}-worker.md`

## Quality Checklist

- [ ] Extensive web research completed on .NET 9 Worker Service patterns
- [ ] MassTransit consumer and saga patterns thoroughly researched
- [ ] Clean Architecture patterns documented and validated
- [ ] EF Core DbContext lifetime management patterns included
- [ ] OpenTelemetry configuration researched and documented
- [ ] All web research findings incorporated into PRP implementation sections
- [ ] Validation commands are executable and technology-appropriate
- [ ] Real-world examples and gotchas captured from research

Score the PRP on a scale of 1-10 (confidence level for creating production-ready Worker Service features based on thorough technology research).

Remember: The goal is creating comprehensive, production-ready Worker Service implementations that leverage Clean Architecture, reliable messaging, and comprehensive observability through extensive research and validation.