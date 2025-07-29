using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkerService.Infrastructure.Data;
using WorkerService.IntegrationTests.Shared.Fixtures;

namespace WorkerService.IntegrationTests.Container.Fixtures;

public class ContainerWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly WorkerServiceTestFixture _fixture;

    public ContainerWebApplicationFactory(WorkerServiceTestFixture fixture)
    {
        _fixture = fixture;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Clear existing configuration sources
            config.Sources.Clear();

            // Add base configuration files
            config.AddJsonFile("appsettings.json", optional: true);
            config.AddJsonFile("appsettings.Test.json", optional: true);
            config.AddJsonFile("Container/appsettings.Test.json", optional: true);

            // Override with container endpoints - NO DI customization, only configuration
            var containerConfig = new Dictionary<string, string>
            {
                // Database configuration
                ["ConnectionStrings:DefaultConnection"] = _fixture.PostgreSqlContainer.GetConnectionString(),
                
                // RabbitMQ configuration
                ["RabbitMq:Host"] = _fixture.RabbitMqContainer.Hostname,
                ["RabbitMq:Port"] = _fixture.RabbitMqContainer.GetMappedPublicPort(5672).ToString(),
                ["RabbitMq:Username"] = "guest",
                ["RabbitMq:Password"] = "guest",
                ["RabbitMq:VirtualHost"] = "/",
                
                // OpenTelemetry configuration
                ["OpenTelemetry:Enabled"] = "true",
                ["OpenTelemetry:ServiceName"] = "WorkerService-ContainerTests",
                ["OpenTelemetry:Otlp:Endpoint"] = _fixture.GetOtlpEndpoint(),
                
                // In-memory settings - disable since we're using containers
                ["InMemory:UseDatabase"] = "false",
                ["InMemory:UseMessageBroker"] = "false",
                
                // Background services configuration
                ["BackgroundServices:OrderSimulator:Enabled"] = "true",
                ["BackgroundServices:OrderSimulator:IntervalMs"] = "500",
                ["BackgroundServices:OrderSimulator:MaxOrders"] = "20",
                ["BackgroundServices:MetricsCollection:Enabled"] = "true",
                ["BackgroundServices:OrderProcessing:Enabled"] = "true",
                
                // Health checks configuration
                ["HealthChecks:Enabled"] = "true",
                
                // Logging configuration for container tests
                ["Logging:LogLevel:Default"] = "Information",
                ["Logging:LogLevel:WorkerService"] = "Debug",
                ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Information",
                ["Logging:LogLevel:MassTransit"] = "Debug"
            };

            config.AddInMemoryCollection(containerConfig.Select(kv => new KeyValuePair<string, string?>(kv.Key, kv.Value)));
        });

        builder.UseEnvironment("Test");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseContentRoot(Directory.GetCurrentDirectory());
        return base.CreateHost(builder);
    }

    public async Task InitializeAsync()
    {
        // Any initialization logic needed for container tests
        // Database migrations, etc. are handled by the application startup
        await Task.CompletedTask;
    }

    public async Task ResetDatabaseAsync()
    {
        // Helper method to reset database state between tests
        // This uses the application's own DbContext without any DI customization
        using var scope = Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;
        
        // Use the application's DbContext without any DI customization
        var dbContext = serviceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Clear all data - this respects the actual application configuration
        dbContext.Orders.RemoveRange(dbContext.Orders);
        await dbContext.SaveChangesAsync();
    }

    public async Task ClearDatabaseAsync()
    {
        // Alias for ResetDatabaseAsync to match the expected interface
        await ResetDatabaseAsync();
    }
}