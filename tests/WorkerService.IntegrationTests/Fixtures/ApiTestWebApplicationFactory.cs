using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using WorkerService.Infrastructure.Data;

namespace WorkerService.IntegrationTests.Fixtures;

/// <summary>
/// Test factory for API integration tests using in-memory database
/// This factory configures the application for API testing with isolated in-memory database
/// </summary>
public class ApiTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private string _databaseName;

    public ApiTestWebApplicationFactory()
    {
        _databaseName = $"TestDb_{Guid.NewGuid()}";
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

            // Configure for API testing with in-memory database
            var testConfig = new Dictionary<string, string>
            {
                // Use in-memory database and message broker
                ["ConnectionStrings:DefaultConnection"] = "InMemory",
                ["InMemory:UseDatabase"] = "true",
                ["InMemory:UseMessageBroker"] = "true",
                
                // Disable OpenTelemetry for testing
                ["OpenTelemetry:Enabled"] = "false",
                
                // Disable background services for API testing
                ["OrderSimulator:Enabled"] = "false",
                ["MetricsCollection:Enabled"] = "false",
                ["OrderProcessing:Enabled"] = "false",
                
                // Configure logging for tests
                ["Logging:LogLevel:Default"] = "Warning",
                ["Logging:LogLevel:Microsoft.EntityFrameworkCore"] = "Warning",
                ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning",
                ["Logging:LogLevel:WorkerService"] = "Information"
            };

            config.AddInMemoryCollection(testConfig.Select(kv => new KeyValuePair<string, string?>(kv.Key, kv.Value)));
        });

        builder.ConfigureServices((context, services) =>
        {
            // Remove all existing DbContext and related services
            var descriptorsToRemove = services.Where(d => 
                d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                d.ServiceType == typeof(DbContextOptions) ||
                d.ServiceType == typeof(ApplicationDbContext) ||
                d.ServiceType?.Name?.Contains("DbContext") == true).ToList();
            
            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            // Add in-memory database for testing with unique name
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            });

            // Remove hosted services that we don't need for API testing
            var hostedServices = services.Where(d => 
                d.ServiceType == typeof(IHostedService) && 
                d.ImplementationType?.Name != "GenericWebHostService").ToList();
            foreach (var service in hostedServices)
            {
                services.Remove(service);
            }

            // Remove all health check services and re-add basic ones for API testing
            var healthCheckServices = services.Where(d => 
                d.ServiceType?.FullName?.Contains("HealthCheck") == true ||
                d.ServiceType?.Name?.Contains("HealthCheck") == true).ToList();
            foreach (var service in healthCheckServices)
            {
                services.Remove(service);
            }

            // Add basic health checks for API testing only
            services.AddHealthChecks(); // Just the basic health check without any custom checks

            // Configure test-specific logging
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddDebug();
                builder.SetMinimumLevel(LogLevel.Warning);
                
                // Enable detailed logging for our services only
                builder.AddFilter("WorkerService", LogLevel.Information);
            });
        });

        builder.UseEnvironment("Test");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Configure the host for API testing
        builder.UseContentRoot(Directory.GetCurrentDirectory());
        
        var host = base.CreateHost(builder);
        
        // Ensure database is created after host is built
        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Database.EnsureCreated();
        
        return host;
    }

    /// <summary>
    /// Clears all data from the database
    /// </summary>
    public async Task ClearDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Clear all entities
        dbContext.Orders.RemoveRange(dbContext.Orders);
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// Resets the database to a clean state
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        await ClearDatabaseAsync();
        
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        
        // Ensure database is in clean state
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
    }

    
}