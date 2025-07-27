using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Xunit;

namespace WorkerService.IntegrationTests.Fixtures;

public class WorkerServiceTestFixture : IAsyncLifetime
{
    public PostgreSqlContainer PostgreSqlContainer { get; private set; }
    public RabbitMqContainer RabbitMqContainer { get; private set; }
    public IContainer JaegerContainer { get; private set; }

    public WorkerServiceTestFixture()
    {
        // Initialize containers with test configurations
        PostgreSqlContainer = new PostgreSqlBuilder()
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .WithPortBinding(5433, 5432) // Use different port to avoid conflicts
            .WithCleanUp(true)
            .Build();

        RabbitMqContainer = new RabbitMqBuilder()
            .WithUsername("guest")
            .WithPassword("guest")
            .WithPortBinding(5673, 5672) // AMQP port
            .WithPortBinding(15673, 15672) // Management UI port
            .WithCleanUp(true)
            .Build();

        JaegerContainer = new ContainerBuilder()
            .WithImage("jaegertracing/all-in-one:latest")
            .WithPortBinding(14251, 14250) // OTLP gRPC port
            .WithPortBinding(16687, 16686) // Jaeger UI port
            .WithEnvironment("COLLECTOR_OTLP_ENABLED", "true")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(14250))
            .WithCleanUp(true)
            .Build();
    }

    public async Task InitializeAsync()
    {
        // Start all containers in parallel for faster startup
        var containerStartTasks = new[]
        {
            PostgreSqlContainer.StartAsync(),
            RabbitMqContainer.StartAsync(),
            JaegerContainer.StartAsync()
        };

        await Task.WhenAll(containerStartTasks);

        // Log container endpoints for debugging
        Console.WriteLine($"PostgreSQL: {PostgreSqlContainer.GetConnectionString()}");
        Console.WriteLine($"RabbitMQ: amqp://guest:guest@{RabbitMqContainer.Hostname}:{RabbitMqContainer.GetMappedPublicPort(5672)}");
        Console.WriteLine($"Jaeger UI: http://localhost:{JaegerContainer.GetMappedPublicPort(16686)}");
        Console.WriteLine($"OTLP Endpoint: http://{JaegerContainer.Hostname}:{JaegerContainer.GetMappedPublicPort(14250)}");
    }

    public async Task DisposeAsync()
    {
        // Dispose all containers in parallel
        var containerDisposeTasks = new List<Task>();

        if (PostgreSqlContainer != null)
            containerDisposeTasks.Add(PostgreSqlContainer.DisposeAsync().AsTask());

        if (RabbitMqContainer != null)
            containerDisposeTasks.Add(RabbitMqContainer.DisposeAsync().AsTask());

        if (JaegerContainer != null)
            containerDisposeTasks.Add(JaegerContainer.DisposeAsync().AsTask());

        await Task.WhenAll(containerDisposeTasks);
    }

    // Helper method to get RabbitMQ connection string
    public string GetRabbitMqConnectionString()
    {
        return $"amqp://guest:guest@{RabbitMqContainer.Hostname}:{RabbitMqContainer.GetMappedPublicPort(5672)}";
    }

    // Helper method to get OTLP endpoint for OpenTelemetry
    public string GetOtlpEndpoint()
    {
        return $"http://{JaegerContainer.Hostname}:{JaegerContainer.GetMappedPublicPort(14250)}";
    }

    // Helper method to get Jaeger UI URL
    public string GetJaegerUiUrl()
    {
        return $"http://localhost:{JaegerContainer.GetMappedPublicPort(16686)}";
    }
}