# Worker Service Feature Request

## FEATURE NAME:

Configurable In-Memory Dependencies for Local Development

---

## FEATURE PURPOSE:

To enable the worker service to run without external dependencies (PostgreSQL, RabbitMQ), facilitating faster local development, debugging, and isolated testing. This will be achieved by implementing a configuration-driven switch to toggle between production-grade providers (PostgreSQL, RabbitMQ) and lightweight in-memory alternatives for Entity Framework Core and MassTransit.

---

## CORE FUNCTIONALITY:

- **Configuration-Driven I/O**: The application's dependency on external services will be controlled via configuration.
- **In-Memory Database**: When configured, the application will use `Microsoft.EntityFrameworkCore.InMemory` instead of Npgsql, eliminating the need for a running PostgreSQL instance.
- **In-Memory Message Bus**: When configured, the application will use the MassTransit in-memory transport instead of RabbitMQ, removing the dependency on a message broker.
- **Strongly-Typed Configuration**: A dedicated configuration class (`InMemorySettings`) will be created to manage these settings, loaded via `IOptions` from environment variables or `appsettings.json`.

---

## CLEAN ARCHITECTURE LAYERS:

This change primarily affects the **Worker Layer** (`WorkerService.Worker`) where services are registered, and the **Infrastructure Layer** (`WorkerService.Infrastructure`) where dependencies are added.

### Worker Layer (`Program.cs`)
- The `Program.cs` file will be modified to read the `InMemorySettings` and conditionally register the appropriate `DbContext` and `IMassTransit` implementations.

### Infrastructure Layer
- A new dependency on `Microsoft.EntityFrameworkCore.InMemory` will be added to the `WorkerService.Infrastructure.csproj` file.

--- 

## CONFIGURATION REQUIREMENTS:

### 1. Strongly-Typed Settings Class
A new class will be created to model the in-memory configuration.

**File:** `src/WorkerService.Worker/Configuration/InMemorySettings.cs`
```csharp
namespace WorkerService.Worker.Configuration;

public class InMemorySettings
{
    public const string SectionName = "InMemory";

    public bool UseDatabase { get; set; } = false;
    public bool UseMessageBroker { get; set; } = false;
}
```

### 2. Environment Variable and `appsettings.json` Configuration
The settings will be configurable via `appsettings.json` and overridden by environment variables for flexibility.

**`appsettings.Development.json`:**
```json
{
  "InMemory": {
    "UseDatabase": false, // Set to true for in-memory DB
    "UseMessageBroker": false // Set to true for in-memory bus
  }
}
```

**Environment Variables:**
- `InMemory__UseDatabase=true`
- `InMemory__UseMessageBroker=true`

### 3. Conditional Service Registration in `Program.cs`
The dependency injection container will be configured based on the loaded settings.

```csharp
// In Program.cs

// ... other builder configuration

var inMemorySettings = builder.Configuration.GetSection(InMemorySettings.SectionName).Get<InMemorySettings>() ?? new InMemorySettings();

// Configure EF Core
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (inMemorySettings.UseDatabase)
    {
        options.UseInMemoryDatabase("WorkerServiceDb");
    }
    else
    {
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
    }
});

// Configure MassTransit
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<OrderCreatedConsumer>();

    if (inMemorySettings.UseMessageBroker)
    {
        x.UsingInMemory((context, cfg) =>
        {
            cfg.ConfigureEndpoints(context);
        });
    }
    else
    {
        x.UsingRabbitMq((context, cfg) =>
        {
            cfg.Host(builder.Configuration.GetValue<string>("RabbitMq:Host"), "/", h =>
            {
                h.Username(builder.Configuration.GetValue<string>("RabbitMq:Username"));
                h.Password(builder.Configuration.GetValue<string>("RabbitMq:Password"));
            });
            cfg.ConfigureEndpoints(context);
        });
    }
});

// ... rest of Program.cs
```

--- 

## PACKAGE MANAGEMENT:

### Required NuGet Packages
- **`WorkerService.Infrastructure` project**:
  - `Microsoft.EntityFrameworkCore.InMemory`

--- 

## TESTING REQUIREMENTS:

- **Manual Verification**: 
  1. Run the application with environment variables `InMemory__UseDatabase=true` and `InMemory__UseMessageBroker=true` while PostgreSQL and RabbitMQ containers are stopped. Verify the application starts and runs without errors.
  2. Run the application without these environment variables, ensuring it connects to the running PostgreSQL and RabbitMQ containers as before.
- **Automated Tests**: Existing integration tests should be updated or new ones created to run against the in-memory providers, ensuring business logic remains correct.

---

## SUCCESS CRITERIA:

- [ ] A `InMemorySettings.cs` class is created in the Worker project.
- [ ] The `Program.cs` file is updated to conditionally register `DbContext` and `IMassTransit` based on `InMemorySettings`.
- [ ] The `Microsoft.EntityFrameworkCore.InMemory` package is added to the `WorkerService.Infrastructure` project.
- [ ] The application can be launched successfully using in-memory providers, without requiring Docker or external services to be running.
- [ ] The application defaults to using PostgreSQL and RabbitMQ when no specific in-memory configuration is provided.
- [ ] Health checks for dependencies (PostgreSQL, RabbitMQ) are conditionally registered, only when the actual dependencies are in use.

---

## EXPECTED COMPLEXITY LEVEL:

- [ ] **Low** - Involves straightforward configuration changes and conditional DI registration.
- [X] **Intermediate** - Production-ready patterns with multiple integrated technologies
- [ ] **Advanced** - Complex scenarios with advanced patterns

**Reasoning:** The task involves modifying the application's startup and configuration logic, touching core parts of the dependency injection setup. While not algorithmically complex, it requires a precise understanding of the .NET Host Builder, `IOptions`, and the configuration of both EF Core and MassTransit.
