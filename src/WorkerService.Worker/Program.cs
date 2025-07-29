using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using WorkerService.Application.Handlers;
using WorkerService.Domain.Interfaces;
using WorkerService.Infrastructure.Consumers;
using WorkerService.Infrastructure.Data;
using WorkerService.Infrastructure.Repositories;
using WorkerService.Worker.Configuration;
using WorkerService.Worker.Endpoints;
using WorkerService.Worker.Health;
using WorkerService.Worker.Services;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();


await CreateAndRunApplication(args);


static async Task CreateAndRunApplication(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog
    builder.Host.UseSerilog((context, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration));

    // Configure strongly-typed settings
    builder.Services.Configure<InMemorySettings>(
        builder.Configuration.GetSection(InMemorySettings.SectionName));
    builder.Services.Configure<BackgroundServicesSettings>(
        builder.Configuration.GetSection(BackgroundServicesSettings.SectionName));
    builder.Services.Configure<OpenTelemetrySettings>(
        builder.Configuration.GetSection(OpenTelemetrySettings.SectionName));
    builder.Services.Configure<HealthCheckSettings>(
        builder.Configuration.GetSection(HealthCheckSettings.SectionName));

    // Get configuration settings for conditional registration
    var inMemorySettings = builder.Configuration
        .GetSection(InMemorySettings.SectionName)
        .Get<InMemorySettings>() ?? new InMemorySettings();
    var backgroundServiceSettings = builder.Configuration
        .GetSection(BackgroundServicesSettings.SectionName)
        .Get<BackgroundServicesSettings>() ?? new BackgroundServicesSettings();
    var openTelemetrySettings = builder.Configuration
        .GetSection(OpenTelemetrySettings.SectionName)
        .Get<OpenTelemetrySettings>() ?? new OpenTelemetrySettings();
    var healthCheckSettings = builder.Configuration
        .GetSection(HealthCheckSettings.SectionName)
        .Get<HealthCheckSettings>() ?? new HealthCheckSettings();

    // Configure OpenTelemetry conditionally based on settings
    if (openTelemetrySettings.Enabled)
    {
        var serviceName = openTelemetrySettings.ServiceName;
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["service.version"] = "1.0.0",
                    ["deployment.environment"] = builder.Environment.EnvironmentName,
                    ["in_memory.database"] = inMemorySettings.UseDatabase,
                    ["in_memory.message_broker"] = inMemorySettings.UseMessageBroker
                }))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(options =>
                {
                    options.EnrichWithHttpRequest = (activity, request) =>
                    {
                        activity.SetTag("http.route", request.Path);
                        activity.SetTag("user.id", request.Headers["X-User-Id"].ToString());
                    };
                    options.EnrichWithHttpResponse = (activity, response) =>
                    {
                        activity.SetTag("http.response.status_code", response.StatusCode);
                    };
                })
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddSqlClientInstrumentation()
                .AddSource("MassTransit")
                .AddSource("OrdersAPI")
                .AddSource("ItemsAPI")
                .AddOtlpExporter())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter("MassTransit")
                .AddMeter("OrdersAPI")
                .AddMeter("ItemsAPI")
                .AddPrometheusExporter());
    }

    // Configure Entity Framework with conditional provider
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
    {
        if (inMemorySettings.UseDatabase)
        {
            // In-memory database for development/testing
            options.UseInMemoryDatabase("WorkerServiceDb");
            options.EnableSensitiveDataLogging(); // Safe for development
        }
        else
        {
            // Production PostgreSQL
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
        }
    });

    // Configure MassTransit with conditional transport
    builder.Services.AddMassTransit(x =>
    {
        // Register consumers
        x.AddConsumer<OrderCreatedConsumer>();

        if (inMemorySettings.UseMessageBroker)
        {
            // In-memory transport for development/testing
            x.UsingInMemory((context, cfg) =>
            {
                cfg.ConfigureEndpoints(context);
            });
        }
        else
        {
            // Production RabbitMQ
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(builder.Configuration.GetConnectionString("RabbitMQ"));

                // Configure receive endpoints with production settings
                cfg.ReceiveEndpoint("order-created", e =>
                {
                    e.SetQuorumQueue(3); // Reliability for production
                    e.ConfigureConsumer<OrderCreatedConsumer>(context);
                });

                cfg.ConfigureEndpoints(context);
            });
        }
    });

    // Register repositories
    builder.Services.AddScoped<IOrderRepository, OrderRepository>();
    builder.Services.AddScoped<IItemRepository, ItemRepository>();

    // Register MediatR
    builder.Services.AddMediatR(typeof(CreateOrderCommandHandler).Assembly);

    // Register FluentValidation
    builder.Services.AddValidatorsFromAssemblyContaining<CreateOrderCommandHandler>();

    // Configure Web API
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    // Configure .NET 9 Native OpenAPI
    builder.Services.AddOpenApi();

    // Add Swagger generation (optional, for development UI)
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "Orders API",
                Version = "v1",
                Description = "RESTful API for Order management in .NET 9 Worker Service"
            });
        });
    }

    // Configure CORS for development
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins("http://localhost:3000", "https://localhost:3001")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
    });

    // Register background services conditionally based on configuration
    if (backgroundServiceSettings.OrderProcessing.Enabled)
    {
        builder.Services.AddHostedService<OrderProcessingService>();
    }

    if (backgroundServiceSettings.MetricsCollection.Enabled)
    {
        builder.Services.AddHostedService<MetricsCollectionService>();
    }

    // Configure health checks conditionally based on settings
    if (healthCheckSettings.Enabled)
    {
        var healthChecksBuilder = builder.Services.AddHealthChecks()
            .AddCheck<WorkerHealthCheck>("worker");

        // Add health checks only for active dependencies
        if (!inMemorySettings.UseDatabase)
        {
            healthChecksBuilder.AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!);
        }        
    }

    var app = builder.Build();

    // Configure middleware pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Orders API v1");
            c.RoutePrefix = "swagger";
        });
        app.UseCors();
    }

    // Log configuration for debugging (conditionally enabled)
    if (builder.Environment.IsDevelopment() || healthCheckSettings.Enabled)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Worker Service Configuration: {Configuration}", inMemorySettings.GetConfigurationSummary());
        logger.LogInformation("Environment: {Environment}", builder.Environment.EnvironmentName);
        logger.LogInformation("OpenTelemetry Enabled: {OpenTelemetryEnabled}", openTelemetrySettings.Enabled);
        logger.LogInformation("Health Checks Enabled: {HealthChecksEnabled}", healthCheckSettings.Enabled);
        logger.LogInformation("Background Services - OrderProcessing: {OrderProcessing}, MetricsCollection: {MetricsCollection}",
            backgroundServiceSettings.OrderProcessing.Enabled, backgroundServiceSettings.MetricsCollection.Enabled);

        if (inMemorySettings.HasInMemoryProviders)
        {
            logger.LogWarning("⚠️  In-memory providers enabled - suitable for development/testing only!");
        }
    }

    // Configure health check endpoints conditionally
    if (healthCheckSettings.Enabled)
    {
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        });
        app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
        {
            Predicate = _ => false
        });
    }

    // Map API endpoints
    app.MapControllers();
    
    // Map Item API endpoints using minimal APIs
    app.MapGroup("/api/items")
        .WithTags("Items")
        .WithOpenApi()
        .MapItemEndpoints();
        
    app.MapOpenApi(); // .NET 9 native OpenAPI endpoint

    // Configure Prometheus metrics endpoint conditionally
    if (openTelemetrySettings.Enabled)
    {
        app.UseOpenTelemetryPrometheusScrapingEndpoint();
    }

    // Ensure database is created
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.EnsureCreatedAsync();
    }

    Log.Information("Starting WorkerService application");
    await app.RunAsync();
}

// Make Program class accessible for integration tests
public partial class Program { }