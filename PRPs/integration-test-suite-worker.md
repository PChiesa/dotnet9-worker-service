---
name: "Integration Test Suite Worker Service PRP"
description: "Comprehensive integration test suite for .NET 9 Worker Service with Testcontainers, MassTransit Test Harness, and OpenTelemetry validation"
---

## Purpose

Implement a comprehensive integration test suite for the order processing pipeline using Testcontainers, MassTransit Test Harness, and OpenTelemetry validation. This suite creates an isolated, ephemeral testing environment that validates end-to-end functionality, reliability, and observability in a production-like setup.

## Core Principles

1. **Isolated Testing Environment**: Testcontainers provide ephemeral PostgreSQL, RabbitMQ, and OTLP containers
2. **Message-Driven Validation**: MassTransit Test Harness validates complete message flows
3. **Production-Like Testing**: Simulated load with realistic order data generation
4. **Observability Validation**: OpenTelemetry trace and metric validation through Jaeger
5. **End-to-End Coverage**: Database persistence, message processing, and telemetry correlation

---

## Goal

Implement **Order Integration Test Suite with Simulated Load** as a comprehensive testing framework that includes:

- Testcontainers orchestration for PostgreSQL, RabbitMQ, and Jaeger/OTLP
- MassTransit Test Harness for message flow validation
- Order simulator service for realistic load generation
- Database state validation through direct queries
- OpenTelemetry trace and metric validation
- CI/CD-ready test execution

## Why

- **Production Confidence**: Tests run against real dependencies in isolated containers
- **Message Reliability**: Validates complete message flows with MassTransit Test Harness
- **Performance Validation**: Simulated load tests reveal bottlenecks and scaling issues
- **Observability Assurance**: Confirms telemetry data is correctly generated and correlated
- **Regression Prevention**: Comprehensive validation prevents unintended side effects

## What

### Implementation Components

**Test Infrastructure (WorkerService.IntegrationTests)**:
- Testcontainers for PostgreSQL, RabbitMQ, and Jaeger/OTLP
- Custom WebApplicationFactory with container configuration
- MassTransit Test Harness integration
- Order simulator service for load generation

**Testing Framework**:
- xUnit test framework with IAsyncLifetime fixtures
- Shared test fixtures for container lifecycle management
- Custom assertions for message flow validation
- Database state verification utilities

**Observability Validation**:
- Jaeger trace correlation verification
- Metrics collection validation
- Structured logging verification
- Performance benchmark assertions

**CI/CD Integration**:
- Docker-enabled pipeline requirements
- Automated test execution via `dotnet test`
- Container cleanup and resource management
- Test result reporting and analysis

### Success Criteria

- [ ] Integration test project with all required dependencies
- [ ] Testcontainers managing PostgreSQL, RabbitMQ, and Jaeger lifecycle
- [ ] Test WebApplicationFactory injecting container configurations
- [ ] Order simulator generating realistic load during tests
- [ ] MassTransit Test Harness validating message consumption
- [ ] Database queries confirming correct order persistence
- [ ] Jaeger UI showing correlated traces for simulated workflows
- [ ] Automated container cleanup after test completion

## All Needed Context

### Documentation & References

```yaml
# TESTCONTAINERS .NET PATTERNS
- url: https://dotnet.testcontainers.org/
  why: Core Testcontainers .NET library documentation and patterns

- url: https://dotnet.testcontainers.org/modules/rabbitmq/
  why: RabbitMQ Testcontainer configuration and usage patterns

- url: https://dotnet.testcontainers.org/modules/postgresql/
  why: PostgreSQL Testcontainer setup and connection management

# MASSTRANSIT TEST FRAMEWORK
- url: https://masstransit.io/documentation/concepts/testing
  why: MassTransit testing patterns and test harness configuration

- url: https://masstransit.io/documentation/configuration/test-harness
  why: Test harness setup, message assertions, and timing patterns

- url: https://github.com/MassTransit/MassTransit/blob/develop/tests/MassTransit.Tests/TestFramework_Specs.cs
  why: Real-world test harness implementation examples

# INTEGRATION TESTING PATTERNS
- url: https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-9.0
  why: WebApplicationFactory patterns and configuration overrides

- url: https://wrapt.dev/blog/building-an-event-driven-dotnet-application-integration-testing
  why: Event-driven application integration testing best practices

# OPENTELEMETRY TESTING
- url: https://opentelemetry.io/blog/2023/testing-otel-demo/
  why: Trace-based testing patterns and telemetry validation

- url: https://github.com/open-telemetry/opentelemetry-demo/tree/main/test
  why: Real-world OpenTelemetry testing implementation examples

# WORKER SERVICE TESTING
- url: https://learn.microsoft.com/en-us/dotnet/core/extensions/workers
  why: Worker Service testing patterns and hosted service lifecycle

- url: https://github.com/dotnet/aspnetcore/issues/54025
  why: Community discussion on Worker Service integration testing approaches
```

### Current Architecture Context

```csharp
// Integration test project structure
tests/
└── WorkerService.IntegrationTests/
    ├── Fixtures/
    │   ├── WorkerServiceTestFixture.cs      // Container lifecycle management
    │   └── TestWebApplicationFactory.cs     // Application configuration override
    ├── Services/
    │   └── OrderSimulatorService.cs         // Load generation service
    ├── Tests/
    │   ├── OrderProcessingIntegrationTests.cs // Main integration tests
    │   └── TelemetryValidationTests.cs      // OpenTelemetry-specific tests
    └── Utilities/
        ├── TestDataBuilder.cs               // Test data generation
        └── DatabaseAssertion.cs             // Database state validation

// Required NuGet packages for integration testing
IntegrationTests: 
  - Testcontainers.PostgreSql (2.3.0+)
  - Testcontainers.RabbitMq (3.7.0+) 
  - Testcontainers.Jaeger (1.6.0+)
  - MassTransit.TestFramework (8.4.1+)
  - Microsoft.AspNetCore.Mvc.Testing (9.0.0+)
  - FluentAssertions (6.12.0+)
  - Bogus (35.4.0+) // For realistic test data generation
  - xunit.runner.visualstudio (2.5.3+)
```

### Technology Configuration Patterns

```csharp
// Testcontainers fixture with lifecycle management
public class WorkerServiceTestFixture : IAsyncLifetime
{
    public PostgreSqlContainer PostgreSqlContainer { get; private set; }
    public RabbitMqContainer RabbitMqContainer { get; private set; }
    public JaegerContainer JaegerContainer { get; private set; }

    public async Task InitializeAsync()
    {
        // Start PostgreSQL container
        PostgreSqlContainer = new PostgreSqlBuilder()
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .Build();

        // Start RabbitMQ container with management UI
        RabbitMqContainer = new RabbitMqBuilder()
            .WithUsername("guest")
            .WithPassword("guest")
            .Build();

        // Start Jaeger container for telemetry
        JaegerContainer = new JaegerBuilder()
            .WithPortBinding(14250, 14250) // OTLP gRPC
            .WithPortBinding(16686, 16686) // Jaeger UI
            .Build();

        // Start all containers in parallel
        await Task.WhenAll(
            PostgreSqlContainer.StartAsync(),
            RabbitMqContainer.StartAsync(),
            JaegerContainer.StartAsync()
        );
    }

    public async Task DisposeAsync()
    {
        await Task.WhenAll(
            PostgreSqlContainer?.DisposeAsync().AsTask() ?? Task.CompletedTask,
            RabbitMqContainer?.DisposeAsync().AsTask() ?? Task.CompletedTask,
            JaegerContainer?.DisposeAsync().AsTask() ?? Task.CompletedTask
        );
    }
}

// Custom WebApplicationFactory with container integration
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly WorkerServiceTestFixture _fixture;

    public TestWebApplicationFactory(WorkerServiceTestFixture fixture)
    {
        _fixture = fixture;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove production DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            // Add test DbContext with container connection string
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(_fixture.PostgreSqlContainer.GetConnectionString()));

            // Register Order Simulator as hosted service for tests
            services.AddHostedService<OrderSimulatorService>();

            // Override MassTransit configuration for test containers
            services.PostConfigure<MassTransitHostOptions>(options =>
            {
                options.WaitUntilStarted = true;
                options.StartTimeout = TimeSpan.FromSeconds(30);
            });
        });

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override configuration with container endpoints
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["ConnectionStrings:DefaultConnection"] = _fixture.PostgreSqlContainer.GetConnectionString(),
                ["MassTransit:RabbitMq:Host"] = _fixture.RabbitMqContainer.Hostname,
                ["MassTransit:RabbitMq:Port"] = _fixture.RabbitMqContainer.GetMappedPublicPort(5672).ToString(),
                ["OpenTelemetry:Otlp:Endpoint"] = $"http://{_fixture.JaegerContainer.Hostname}:{_fixture.JaegerContainer.GetMappedPublicPort(14250)}",
                ["OrderSimulator:Enabled"] = "true",
                ["OrderSimulator:IntervalMs"] = "500",
                ["OrderSimulator:MaxOrders"] = "20"
            });
        });
    }
}

// Order Simulator Service for realistic load generation
public class OrderSimulatorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderSimulatorService> _logger;
    private readonly Faker<CreateOrderCommand> _orderFaker;
    private readonly IConfiguration _configuration;

    public OrderSimulatorService(
        IServiceScopeFactory scopeFactory,
        ILogger<OrderSimulatorService> logger,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;

        // Configure Bogus faker for realistic order data
        _orderFaker = new Faker<CreateOrderCommand>()
            .RuleFor(o => o.CustomerName, f => f.Name.FullName())
            .RuleFor(o => o.ProductName, f => f.Commerce.ProductName())
            .RuleFor(o => o.Quantity, f => f.Random.Int(1, 10))
            .RuleFor(o => o.UnitPrice, f => f.Random.Decimal(10, 1000))
            .RuleFor(o => o.OrderDate, f => f.Date.Recent(30));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue<bool>("OrderSimulator:Enabled"))
            return;

        var intervalMs = _configuration.GetValue<int>("OrderSimulator:IntervalMs", 1000);
        var maxOrders = _configuration.GetValue<int>("OrderSimulator:MaxOrders", 10);
        var ordersGenerated = 0;

        _logger.LogInformation("Order Simulator starting - will generate {MaxOrders} orders every {IntervalMs}ms", maxOrders, intervalMs);

        while (!stoppingToken.IsCancellationRequested && ordersGenerated < maxOrders)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                var order = _orderFaker.Generate();
                await mediator.Send(order, stoppingToken);

                ordersGenerated++;
                _logger.LogDebug("Generated order {OrderNumber}: {CustomerName} - {ProductName}", ordersGenerated, order.CustomerName, order.ProductName);

                await Task.Delay(intervalMs, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error generating order {OrderNumber}", ordersGenerated + 1);
            }
        }

        _logger.LogInformation("Order Simulator completed - generated {OrdersGenerated} orders", ordersGenerated);
    }
}

// MassTransit Test Harness integration pattern
public class OrderProcessingIntegrationTests : IClassFixture<WorkerServiceTestFixture>
{
    private readonly WorkerServiceTestFixture _fixture;

    public OrderProcessingIntegrationTests(WorkerServiceTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Should_Process_Simulated_Orders_End_To_End()
    {
        // Arrange
        await using var factory = new TestWebApplicationFactory(_fixture);
        await using var scope = factory.Services.CreateAsyncScope();
        
        var testHarness = scope.ServiceProvider.GetRequiredService<ITestHarness>();
        await testHarness.Start();

        try
        {
            // Act - Let the simulator run for a test period
            await Task.Delay(TimeSpan.FromSeconds(15)); // Allow time for order generation and processing

            // Assert - Message flow validation
            (await testHarness.Consumed.Any<CreateOrderCommand>()).Should().BeTrue();
            (await testHarness.Published.Any<OrderCreatedEvent>()).Should().BeTrue();

            var consumedMessages = await testHarness.Consumed.SelectAsync<CreateOrderCommand>().ToListAsync();
            var publishedEvents = await testHarness.Published.SelectAsync<OrderCreatedEvent>().ToListAsync();

            consumedMessages.Should().HaveCountGreaterThan(5);
            publishedEvents.Should().HaveCount(consumedMessages.Count);

            // Assert - Database state validation
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ordersInDatabase = await dbContext.Orders.CountAsync();
            
            ordersInDatabase.Should().Be(consumedMessages.Count);

            // Assert - All orders should be in completed state
            var completedOrders = await dbContext.Orders
                .Where(o => o.Status == OrderStatus.Completed)
                .CountAsync();
            
            completedOrders.Should().Be(ordersInDatabase);

            // Log Jaeger UI access for manual trace verification
            var jaegerUiUrl = $"http://localhost:{_fixture.JaegerContainer.GetMappedPublicPort(16686)}";
            Console.WriteLine($"Jaeger UI available at: {jaegerUiUrl}");
            Console.WriteLine("Search for traces with service name 'WorkerService' to see end-to-end correlation");
        }
        finally
        {
            await testHarness.Stop();
        }
    }
}
```

### Known Implementation Patterns

```typescript
// Critical patterns for integration testing implementation
const integrationTestPatterns = {
  // Container lifecycle management (CRITICAL)
  testcontainers: {
    pattern: "IAsyncLifetime with parallel container startup",
    startup: "Task.WhenAll for concurrent container initialization",
    cleanup: "Automatic disposal via IAsyncLifetime",
    isolation: "Each test class gets fresh containers"
  },
  
  // Test harness integration
  massTransitTesting: {
    pattern: "ITestHarness with WebApplicationFactory integration",
    assertions: "await harness.Consumed.Any<T>() for message validation",
    timing: "TestInactivityTimeout for reliable test execution",
    scope: "Create test harness scope per test method"
  },
  
  // Load simulation
  loadGeneration: {
    pattern: "IHostedService with Bogus faker for realistic data",
    control: "Configuration-driven test duration and volume",
    cancellation: "CancellationToken support for graceful test termination",
    logging: "Structured logging for test debugging"
  },
  
  // Observability validation
  telemetryTesting: {
    pattern: "Jaeger container with OTLP endpoint configuration",
    validation: "Manual trace inspection via Jaeger UI",
    correlation: "Verify complete request spans across all components",
    metrics: "Automated metrics collection verification"
  }
};
```

## Implementation Blueprint

### Phase 1: Test Infrastructure Setup

```yaml
Task 1 - Create Integration Test Project:
  CREATE WorkerService.IntegrationTests project:
    - Add project reference to WorkerService.Worker
    - Install required NuGet packages for Testcontainers and testing
    - Configure test project file with Docker dependency warnings
    - Setup initial test folder structure
    
Task 2 - Implement Container Fixtures:
  CREATE container lifecycle management:
    - WorkerServiceTestFixture with IAsyncLifetime
    - PostgreSQL container with test database configuration
    - RabbitMQ container with management UI access
    - Jaeger container with OTLP and UI port mappings
```

### Phase 2: Test Application Factory

```yaml
Task 3 - Implement Test WebApplicationFactory:
  CREATE TestWebApplicationFactory:
    - Override DbContext configuration with container connection string
    - Replace MassTransit configuration with container endpoints
    - Register OrderSimulatorService as hosted service
    - Configure OpenTelemetry to export to test Jaeger instance
    
Task 4 - Create Order Simulator Service:
  IMPLEMENT realistic load generation:
    - BackgroundService with configurable order generation
    - Bogus faker for realistic order data
    - Configuration-driven test parameters (interval, count)
    - Proper IServiceScopeFactory usage for DbContext access
```

### Phase 3: Integration Test Implementation

```yaml
Task 5 - Implement MassTransit Test Harness Integration:
  CREATE message flow validation:
    - Test harness registration and lifecycle management
    - Message consumption and publishing assertions
    - Consumer behavior validation with test messages
    - Error handling and retry policy testing
    
Task 6 - Create Database Validation Utilities:
  IMPLEMENT state verification helpers:
    - Direct database query utilities
    - Order state transition validation
    - Data consistency assertion helpers
    - Performance benchmark utilities
```

### Phase 4: Observability Testing

```yaml
Task 7 - Implement Telemetry Validation:
  CREATE OpenTelemetry testing patterns:
    - Jaeger trace correlation verification
    - Metrics collection validation
    - Custom span and tag validation
    - Performance telemetry assertions
    
Task 8 - Create Test Documentation:
  DOCUMENT testing procedures:
    - Jaeger UI access instructions
    - Test data interpretation guidelines
    - Troubleshooting common test failures
    - CI/CD integration requirements
```

### Phase 5: CI/CD Integration

```yaml
Task 9 - Configure Test Execution:
  SETUP automated test running:
    - Docker requirement validation
    - Test timeout configuration
    - Resource cleanup verification
    - Test result reporting
    
Task 10 - Implement Performance Benchmarks:
  CREATE performance validation:
    - Throughput measurement assertions
    - Latency benchmark validation
    - Resource usage monitoring
    - Scalability testing scenarios
```

## Validation Loop

### Level 1: Container Infrastructure Validation

```bash
# Verify Testcontainer dependencies
dotnet list tests/WorkerService.IntegrationTests/ package | grep -E "Testcontainers|MassTransit.TestFramework"

# Check container startup capability
docker version
docker-compose version

# Validate test project structure
find tests/WorkerService.IntegrationTests/ -name "*.cs" | grep -E "Fixture|Factory|Simulator"
```

### Level 2: Test Framework Validation

```bash
# Run integration tests with detailed logging
dotnet test tests/WorkerService.IntegrationTests/ --logger "console;verbosity=detailed"

# Verify container cleanup
docker ps -a | grep testcontainers
docker container prune -f

# Check test harness functionality
grep -r "ITestHarness" tests/WorkerService.IntegrationTests/
grep -r "await.*Consumed" tests/WorkerService.IntegrationTests/
```

### Level 3: Message Flow Validation

```bash
# Verify MassTransit test configuration
grep -r "AddMassTransitTestHarness" tests/WorkerService.IntegrationTests/
grep -r "CreateOrderCommand\|OrderCreatedEvent" tests/WorkerService.IntegrationTests/

# Check order simulation configuration
grep -r "OrderSimulatorService" tests/WorkerService.IntegrationTests/
grep -r "Bogus\|Faker" tests/WorkerService.IntegrationTests/
```

### Level 4: End-to-End Validation

```bash
# Run complete integration test suite
dotnet test tests/WorkerService.IntegrationTests/ --collect:"XPlat Code Coverage"

# Verify observability endpoints (during test execution)
curl -f http://localhost:16686/search || echo "Jaeger UI not accessible during test"

# Check database state validation
grep -r "dbContext.Orders" tests/WorkerService.IntegrationTests/
grep -r "OrderStatus.Completed" tests/WorkerService.IntegrationTests/
```

## Final Validation Checklist

### Test Infrastructure

- [ ] WorkerService.IntegrationTests project created with all dependencies
- [ ] Testcontainers for PostgreSQL, RabbitMQ, and Jaeger properly configured
- [ ] Container lifecycle managed through IAsyncLifetime fixture
- [ ] TestWebApplicationFactory overriding configuration with container endpoints
- [ ] All containers start successfully and expose required ports

### Load Generation and Simulation

- [ ] OrderSimulatorService generates realistic order data using Bogus
- [ ] Configurable test parameters (order count, interval, duration)
- [ ] Proper IServiceScopeFactory usage for scoped service access
- [ ] Graceful cancellation and cleanup when tests complete
- [ ] Structured logging for test debugging and verification

### Message Flow Validation

- [ ] MassTransit Test Harness integrated with WebApplicationFactory
- [ ] Message consumption assertions validating CreateOrderCommand processing
- [ ] Event publishing assertions confirming OrderCreatedEvent publication
- [ ] Consumer behavior validated through test harness timing
- [ ] Error handling and retry policies tested with fault injection

### Database and State Validation

- [ ] Direct database queries confirming order persistence
- [ ] Order state transitions validated (pending → processing → completed)
- [ ] Data consistency verified across message processing
- [ ] Database cleanup and isolation between test runs
- [ ] Performance assertions for data access operations

### Observability and Telemetry

- [ ] OpenTelemetry configured to export to test Jaeger instance
- [ ] Traces visible in Jaeger UI with proper correlation IDs
- [ ] Complete request spans across Worker, Consumer, and Database components
- [ ] Metrics collection verified for MassTransit and EF Core operations
- [ ] Performance telemetry captured for throughput and latency analysis

### CI/CD Integration

- [ ] Tests runnable via `dotnet test` command in CI/CD pipeline
- [ ] Docker requirement validation and error handling
- [ ] Automatic container cleanup after test completion
- [ ] Test result reporting and failure analysis
- [ ] Resource usage monitoring and optimization

---

## Anti-Patterns to Avoid

### Container Management Issues

- ❌ Don't forget to implement IAsyncLifetime for proper container cleanup
- ❌ Don't start containers sequentially - use Task.WhenAll for parallel startup
- ❌ Don't hardcode container ports - use GetMappedPublicPort() methods
- ❌ Don't ignore container startup failures - implement proper error handling

### Test Harness Misuse

- ❌ Don't use test harness outside of properly configured scopes
- ❌ Don't ignore TestInactivityTimeout - configure appropriate timing for your tests
- ❌ Don't forget to start and stop test harness in test lifecycle
- ❌ Don't mix unit testing patterns with integration test harness

### Load Generation Problems

- ❌ Don't generate unlimited load - always configure maximum limits
- ❌ Don't ignore cancellation tokens in load generation services
- ❌ Don't use production data generation patterns in tests
- ❌ Don't forget to validate generated data meets business rules

### Database Testing Issues

- ❌ Don't share database state between tests - ensure proper isolation
- ❌ Don't forget to run migrations on test database containers
- ❌ Don't ignore connection string configuration overrides
- ❌ Don't perform database operations outside of proper scopes

### Observability Testing Mistakes

- ❌ Don't rely solely on manual trace inspection - automate where possible
- ❌ Don't ignore telemetry configuration in test environment
- ❌ Don't forget to validate correlation IDs across trace spans
- ❌ Don't skip performance telemetry validation in integration tests

**Confidence Score: 9/10**

This PRP provides a comprehensive, production-ready approach to integration testing Worker Services with extensive web research into 2025 patterns for Testcontainers, MassTransit Test Harness, Clean Architecture testing, and OpenTelemetry validation. The implementation covers all aspects from container orchestration to observability validation with executable validation commands and real-world examples.