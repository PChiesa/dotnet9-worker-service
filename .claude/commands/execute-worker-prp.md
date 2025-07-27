# Execute Worker Service PRP

Execute a comprehensive Worker Service PRP to implement .NET 9 background processing features with Clean Architecture, MassTransit, PostgreSQL, and OpenTelemetry.

## PRP File: {filename}

## Execution Process

1. **Load Worker Service PRP**
   - Read the specified Worker Service PRP file completely
   - Understand all Clean Architecture layer requirements
   - Review MassTransit messaging patterns and consumer implementations
   - Note EF Core database integration and DbContext scoping requirements
   - Understand OpenTelemetry observability requirements

2. **ULTRATHINK - Clean Architecture Design**
   - Plan Domain layer entities, value objects, and interfaces
   - Design Application layer CQRS handlers and business logic
   - Plan Infrastructure layer repositories, DbContext, and MassTransit consumers
   - Design Worker layer BackgroundServices and Program.cs configuration
   - Map all dependencies and ensure proper layer separation

3. **Implement Clean Architecture Solution**
   - Create Domain layer with entities and interfaces (no external dependencies)
   - Implement Application layer with MediatR handlers and validation
   - Build Infrastructure layer with EF Core, repositories, and MassTransit consumers
   - Develop Worker layer with BackgroundServices and comprehensive configuration
   - Ensure proper dependency injection and lifetime management

4. **Configure Technology Stack**
   - Set up MassTransit with RabbitMQ transport and consumer registration
   - Configure EF Core with PostgreSQL and proper DbContext scoping
   - Implement OpenTelemetry with all required instrumentations
   - Add health checks for RabbitMQ, PostgreSQL, and custom worker status
   - Create Docker Compose for local development environment

5. **Implement Testing Strategy**
   - Create unit tests for Domain entities and Application handlers
   - Implement integration tests using MassTransit TestFramework
   - Add health check validation tests
   - Create end-to-end workflow tests with database integration

6. **Validate Implementation**
   - Run all validation commands specified in the PRP
   - Verify Clean Architecture compliance and layer separation
   - Test MassTransit consumer functionality and message processing
   - Validate EF Core DbContext lifetime management
   - Confirm OpenTelemetry instrumentation and health check functionality

## Implementation Requirements

### Clean Architecture Structure
```
src/
├── WorkerService.Domain/
│   ├── Entities/
│   ├── ValueObjects/
│   ├── Interfaces/
│   └── Events/
├── WorkerService.Application/
│   ├── Commands/
│   ├── Queries/
│   ├── Handlers/
│   ├── Services/
│   └── Validators/
├── WorkerService.Infrastructure/
│   ├── Data/
│   ├── Repositories/
│   ├── Consumers/
│   └── Services/
└── WorkerService.Worker/
    ├── Services/
    ├── Health/
    └── Program.cs
```

### Required Implementations

**Domain Layer** (no external dependencies):
- Entity classes with business logic
- Value objects for type safety
- Repository interfaces
- Domain event definitions

**Application Layer** (depends only on Domain):
- Command and Query DTOs
- MediatR command/query handlers
- FluentValidation validators
- Application service interfaces

**Infrastructure Layer** (depends on Application and Domain):
- ApplicationDbContext with PostgreSQL configuration
- Repository implementations
- MassTransit consumer implementations
- External service integrations

**Worker Layer** (depends on Infrastructure and Application):
- BackgroundService implementations with proper scoping
- Program.cs with complete service registration
- Health check implementations
- Configuration management

### Technology Configuration Standards

**MassTransit Setup**:
```csharp
services.AddMassTransit(x =>
{
    x.AddConsumer<YourConsumer>();
    
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("rabbitmq://localhost");
        
        cfg.ReceiveEndpoint("your-queue", e =>
        {
            e.SetQuorumQueue(3); // Reliability
            e.ConfigureConsumer<YourConsumer>(context);
        });
    });
});
```

**EF Core Configuration**:
```csharp
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
```

**OpenTelemetry Configuration**:
```csharp
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

**Health Checks Configuration**:
```csharp
builder.Services.AddHealthChecks()
    .AddRabbitMQ()
    .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection"))
    .AddCheck<WorkerHealthCheck>("worker");
```

### BackgroundService Implementation Pattern

**CRITICAL - Always use this pattern for DbContext in BackgroundServices**:
```csharp
public class YourBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    
    public YourBackgroundService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            // Your background work here
            
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
```

## Validation Requirements

### Architecture Validation
```bash
# Verify Clean Architecture structure
find src/ -name "*.csproj" | sort
dotnet sln list

# Check dependency rules
dotnet list src/WorkerService.Domain/ reference  # Should be empty
dotnet list src/WorkerService.Application/ reference  # Only Domain
dotnet list src/WorkerService.Infrastructure/ reference  # Application and Domain
dotnet list src/WorkerService.Worker/ reference  # Infrastructure and Application
```

### Build and Test Validation
```bash
# Build verification
dotnet restore
dotnet build --no-restore
dotnet test --no-build

# Package verification
dotnet list package | grep -E "MassTransit|Npgsql|OpenTelemetry|HealthChecks"
```

### Technology Integration Validation
```bash
# MassTransit configuration check
grep -r "AddMassTransit" src/WorkerService.Worker/
grep -r "IConsumer" src/WorkerService.Infrastructure/

# EF Core configuration check
grep -r "UseNpgsql" src/WorkerService.Infrastructure/
grep -r "IServiceScopeFactory" src/WorkerService.Worker/

# OpenTelemetry configuration check
grep -r "AddOpenTelemetry" src/WorkerService.Worker/
grep -E "AddSource.*MassTransit" src/WorkerService.Worker/

# Health checks validation
grep -r "AddHealthChecks" src/WorkerService.Worker/
```

### Runtime Validation
```bash
# Start dependencies
docker-compose up -d

# Test health endpoint
curl http://localhost:5000/health

# Check service startup
dotnet run --project src/WorkerService.Worker/
```

## Success Criteria

- [ ] Clean Architecture structure implemented correctly
- [ ] All layer dependencies follow proper direction (Domain <- Application <- Infrastructure <- Worker)
- [ ] MassTransit configured with RabbitMQ transport and consumer registration
- [ ] EF Core configured with PostgreSQL and proper DbContext scoping
- [ ] OpenTelemetry instrumentation configured for all components
- [ ] Health checks implemented for all dependencies
- [ ] Unit and integration tests created and passing
- [ ] Docker Compose environment functional
- [ ] Background services implement proper cancellation and error handling
- [ ] All validation commands pass successfully

## Quality Gates

### Code Quality
- [ ] No circular dependencies between projects
- [ ] Repository pattern correctly implemented
- [ ] CQRS pattern with MediatR working
- [ ] Proper async/await usage throughout
- [ ] Cancellation token support implemented

### Messaging Quality
- [ ] MassTransit consumers properly registered
- [ ] Message routing configured correctly
- [ ] Error handling and retry policies implemented
- [ ] Idempotent consumer patterns followed

### Data Access Quality
- [ ] DbContext lifetime properly managed in background services
- [ ] Repository pattern abstracts data access
- [ ] Migration strategy implemented
- [ ] Connection string management secure

### Observability Quality
- [ ] OpenTelemetry tracing working across all components
- [ ] Metrics collected and exportable
- [ ] Structured logging with correlation IDs
- [ ] Health checks accurately reflect dependency status

### Operational Quality
- [ ] Graceful shutdown implemented
- [ ] Configuration externalized
- [ ] Docker Compose starts all dependencies
- [ ] Local development environment functional

Note: If any validation fails, analyze the error, fix the implementation, and re-validate until all criteria pass. The implementation must be production-ready with proper Clean Architecture compliance, reliable messaging, and comprehensive observability.