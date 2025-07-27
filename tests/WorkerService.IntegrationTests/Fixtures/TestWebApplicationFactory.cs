using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkerService.Infrastructure.Data;
using WorkerService.IntegrationTests.Services;

namespace WorkerService.IntegrationTests.Fixtures;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly WorkerServiceTestFixture _fixture;

    public TestWebApplicationFactory(WorkerServiceTestFixture fixture)
    {
        _fixture = fixture;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Remove any existing configuration sources
            config.Sources.Clear();

            // Add base configuration
            config.AddJsonFile("appsettings.json", optional: true);
            config.AddJsonFile("appsettings.Test.json", optional: true);

            // Override with container endpoints
            var testConfig = new Dictionary<string, string>
            {
                ["ConnectionStrings:DefaultConnection"] = _fixture.PostgreSqlContainer.GetConnectionString(),
                ["MassTransit:RabbitMq:Host"] = _fixture.RabbitMqContainer.Hostname,
                ["MassTransit:RabbitMq:Port"] = _fixture.RabbitMqContainer.GetMappedPublicPort(5672).ToString(),
                ["MassTransit:RabbitMq:Username"] = "guest",
                ["MassTransit:RabbitMq:Password"] = "guest",
                ["MassTransit:RabbitMq:VirtualHost"] = "/",
                ["OpenTelemetry:Otlp:Endpoint"] = _fixture.GetOtlpEndpoint(),
                ["OpenTelemetry:ServiceName"] = "WorkerService-IntegrationTests",
                ["OrderSimulator:Enabled"] = "true",
                ["OrderSimulator:IntervalMs"] = "500",
                ["OrderSimulator:MaxOrders"] = "20",
                ["Logging:LogLevel:Default"] = "Information",
                ["Logging:LogLevel:MassTransit"] = "Debug",
                ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Warning"
            };

            config.AddInMemoryCollection(testConfig.Select(kv => new KeyValuePair<string, string?>(kv.Key, kv.Value)));
        });

        builder.ConfigureServices((context, services) =>
        {
            // Remove the existing DbContext registration
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            // Add test DbContext with container connection string
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseNpgsql(_fixture.PostgreSqlContainer.GetConnectionString());
                options.EnableSensitiveDataLogging(); // Enable for debugging
                options.EnableDetailedErrors();
            });

            // Add MassTransit Test Harness
            services.AddMassTransitTestHarness();

            // Override MassTransit timeouts for tests
            services.PostConfigure<MassTransitHostOptions>(options =>
            {
                options.WaitUntilStarted = true;
                options.StartTimeout = TimeSpan.FromSeconds(30);
                options.StopTimeout = TimeSpan.FromSeconds(10);
            });

            // Register the order simulator service (only for tests that need it)
            if (context.Configuration.GetValue<bool>("OrderSimulator:Enabled"))
            {
                services.AddHostedService<OrderSimulatorService>();
            }

            // Add test-specific logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddDebug();
                builder.SetMinimumLevel(LogLevel.Debug);
            });
        });

        builder.UseEnvironment("Test");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Ensure we're using the Worker SDK
        builder.UseContentRoot(Directory.GetCurrentDirectory());
        
        return base.CreateHost(builder);
    }

    public async Task InitializeAsync()
    {
        // Ensure database is created and migrations are applied
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        await dbContext.Database.EnsureDeletedAsync(); // Clean slate for tests
        await dbContext.Database.MigrateAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        // Helper method to reset database state between tests
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Clear all data
        dbContext.Orders.RemoveRange(dbContext.Orders);
        await dbContext.SaveChangesAsync();
    }
}