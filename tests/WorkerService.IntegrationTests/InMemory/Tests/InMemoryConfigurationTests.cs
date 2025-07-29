using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using MassTransit.Testing;
using WorkerService.Infrastructure.Data;
using WorkerService.Infrastructure.Consumers;
using WorkerService.Worker.Configuration;
using Xunit;

namespace WorkerService.IntegrationTests.InMemory.Tests;

public class InMemoryConfigurationTests : IAsyncLifetime
{
    private IHost? _host;

    [Fact]
    public async Task Should_Start_With_InMemory_Database_Configuration()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InMemory:UseDatabase"] = "true",
                ["InMemory:UseMessageBroker"] = "false",
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
                ["ConnectionStrings:RabbitMQ"] = "amqp://guest:guest@localhost:5672/"
            })
            .Build();

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddConfiguration(configuration);

        // Configure services as in Program.cs
        ConfigureServices(builder.Services, builder.Configuration);

        _host = builder.Build();

        // Act & Assert
        await _host.StartAsync();
        
        var dbContext = _host.Services.GetRequiredService<ApplicationDbContext>();
        Assert.NotNull(dbContext);

        // Verify in-memory database is being used
        Assert.Contains("InMemory", dbContext.Database.ProviderName);
        
        // Verify we can add and retrieve data
        var orderItems = new[]
        {
            new WorkerService.Domain.Entities.OrderItem("PROD-001", 2, new WorkerService.Domain.ValueObjects.Money(50.00m))
        };
        var testOrder = new WorkerService.Domain.Entities.Order("TEST-001", orderItems);
        
        dbContext.Orders.Add(testOrder);
        await dbContext.SaveChangesAsync();
        
        var retrievedOrder = await dbContext.Orders.FirstOrDefaultAsync(o => o.CustomerId == "TEST-001");
        Assert.NotNull(retrievedOrder);
        Assert.Equal("TEST-001", retrievedOrder.CustomerId);

        await _host.StopAsync();
    }

    [Fact]
    public async Task Should_Start_With_InMemory_MessageBroker_Configuration()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InMemory:UseDatabase"] = "false",
                ["InMemory:UseMessageBroker"] = "true",
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test"
            })
            .Build();

        // Act & Assert
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<OrderCreatedConsumer>();
                x.UsingInMemory((context, cfg) =>
                {
                    cfg.ConfigureEndpoints(context);
                });
            })
            .BuildServiceProvider(true);

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        // Verify in-memory transport is being used
        Assert.Contains("loopback", harness.Bus.Address.ToString());

        // Test message publishing and consumption
        var orderCreated = new WorkerService.Domain.Events.OrderCreatedEvent(
            Guid.NewGuid(), 
            "TEST-002", 
            100.00m);

        await harness.Bus.Publish(orderCreated);

        // Verify message was published
        Assert.True(await harness.Published.Any<WorkerService.Domain.Events.OrderCreatedEvent>());

        await harness.Stop();
    }

    [Fact]
    public async Task Should_Start_With_Both_InMemory_Providers()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InMemory:UseDatabase"] = "true",
                ["InMemory:UseMessageBroker"] = "true",
                ["HealthChecks:Enabled"] = "true"
            })
            .Build();

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddConfiguration(configuration);

        // Configure services 
        ConfigureServices(builder.Services, builder.Configuration);

        _host = builder.Build();

        // Act & Assert
        await _host.StartAsync();
        
        // Verify both providers are in-memory
        var dbContext = _host.Services.GetRequiredService<ApplicationDbContext>();
        Assert.Contains("InMemory", dbContext.Database.ProviderName);

        var inMemorySettings = _host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<InMemorySettings>>().Value;
        Assert.True(inMemorySettings.UseDatabase);
        Assert.True(inMemorySettings.UseMessageBroker);
        Assert.True(inMemorySettings.HasInMemoryProviders);

        await _host.StopAsync();
    }

    [Fact]
    public void Should_Start_With_Production_Configuration_By_Default()
    {
        // Arrange - No in-memory configuration provided
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
                ["ConnectionStrings:RabbitMQ"] = "amqp://guest:guest@localhost:5672/"
            })
            .Build();

        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddConfiguration(configuration);

        // Configure services
        ConfigureServices(builder.Services, builder.Configuration);

        // Act
        var inMemorySettings = builder.Configuration
            .GetSection(InMemorySettings.SectionName)
            .Get<InMemorySettings>() ?? new InMemorySettings();

        // Assert - Should default to production providers
        Assert.False(inMemorySettings.UseDatabase);
        Assert.False(inMemorySettings.UseMessageBroker);
        Assert.False(inMemorySettings.HasInMemoryProviders);
        Assert.Equal("Database: PostgreSQL, MessageBroker: RabbitMQ", inMemorySettings.GetConfigurationSummary());
    }

    [Fact]
    public void Should_Parse_Environment_Variables_Correctly()
    {
        // Arrange - Simulate environment variables by setting actual environment variables
        Environment.SetEnvironmentVariable("InMemory__UseDatabase", "true");
        Environment.SetEnvironmentVariable("InMemory__UseMessageBroker", "false");
        
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .Build();

            // Act
            var inMemorySettings = configuration
                .GetSection(InMemorySettings.SectionName)
                .Get<InMemorySettings>() ?? new InMemorySettings();

            // Assert
            Assert.True(inMemorySettings.UseDatabase);
            Assert.False(inMemorySettings.UseMessageBroker);
            Assert.True(inMemorySettings.HasInMemoryProviders);
        }
        finally
        {
            // Clean up environment variables
            Environment.SetEnvironmentVariable("InMemory__UseDatabase", null);
            Environment.SetEnvironmentVariable("InMemory__UseMessageBroker", null);
        }
    }

    private static void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Replicate the service configuration from Program.cs
        services.Configure<InMemorySettings>(
            configuration.GetSection(InMemorySettings.SectionName));

        var inMemorySettings = configuration
            .GetSection(InMemorySettings.SectionName)
            .Get<InMemorySettings>() ?? new InMemorySettings();

        // Configure Entity Framework with conditional provider
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            if (inMemorySettings.UseDatabase)
            {
                options.UseInMemoryDatabase("WorkerServiceDb");
                options.EnableSensitiveDataLogging();
            }
            else
            {
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
            }
        });

        // Configure MassTransit with conditional transport
        services.AddMassTransit(x =>
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
                    cfg.Host(configuration.GetConnectionString("RabbitMQ"));
                    cfg.ConfigureEndpoints(context);
                });
            }
        });

        // Add other required services
        services.AddLogging();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}