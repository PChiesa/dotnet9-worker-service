using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WorkerService.Infrastructure.Data;
using WorkerService.Infrastructure.Consumers;

namespace WorkerService.IntegrationTests.InMemory.Fixtures;

public class InMemoryWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Clear existing configuration sources
            config.Sources.Clear();

            // Add base configuration files
            config.AddJsonFile("appsettings.json", optional: true);
            config.AddJsonFile("appsettings.Test.json", optional: false);
        });

        builder.ConfigureTestServices(services =>
        {
            // Remove all existing MassTransit related services
            var massTransitServices = services.Where(d => 
                d.ServiceType.Namespace != null && 
                d.ServiceType.Namespace.StartsWith("MassTransit")).ToList();
            
            foreach (var service in massTransitServices)
            {
                services.Remove(service);
            }

            // Add MassTransit test harness with proper configuration
            services.AddMassTransitTestHarness(cfg =>
            {
                // Register the same consumers as in Program.cs
                cfg.AddConsumer<OrderCreatedConsumer>();
                cfg.AddConsumer<OrderPaidConsumer>();
                cfg.AddConsumer<OrderShippedConsumer>();
                cfg.AddConsumer<OrderDeliveredConsumer>();
                cfg.AddConsumer<OrderCancelledConsumer>();
            });
        });

        builder.UseEnvironment("Test");
    }

    public async Task ClearDatabaseAsync()
    {
        // Helper method to clear database data between tests
        // This uses the application's own DbContext without any DI customization
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetService<ApplicationDbContext>();
        if (dbContext != null)
        {
            // Ensure database is in clean state using application's own logic
            await dbContext.Database.EnsureDeletedAsync();
            await dbContext.Database.EnsureCreatedAsync();
        }
    }
}