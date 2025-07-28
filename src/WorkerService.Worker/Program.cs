using FluentValidation;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Instrumentation.EntityFrameworkCore;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Instrumentation.SqlClient;
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
using WorkerService.Worker.Health;
using WorkerService.Worker.Services;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

// For testing environments, don't catch exceptions to allow test framework to handle them
var isTestEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Test" ||
                       Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Test";

if (!isTestEnvironment)
{
    try
    {
        await CreateAndRunApplication(args);
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Application terminated unexpectedly");
    }
    finally
    {
        Log.CloseAndFlush();
    }
}
else
{
    // In test environment, let exceptions bubble up for proper test diagnostics
    await CreateAndRunApplication(args);
}

static async Task CreateAndRunApplication(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog
    builder.Host.UseSerilog((context, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration));

    // Configure strongly-typed settings
    builder.Services.Configure<InMemorySettings>(
        builder.Configuration.GetSection(InMemorySettings.SectionName));

    // Get in-memory settings for conditional registration
    var inMemorySettings = builder.Configuration
        .GetSection(InMemorySettings.SectionName)
        .Get<InMemorySettings>() ?? new InMemorySettings();

    // Configure OpenTelemetry with in-memory configuration metadata
    const string serviceName = "WorkerService";
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
            .AddOtlpExporter())
        .WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddMeter("MassTransit")
            .AddMeter("OrdersAPI")
            .AddPrometheusExporter());

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

    // Register background services
    builder.Services.AddHostedService<OrderProcessingService>();
    builder.Services.AddHostedService<MetricsCollectionService>();

    // Configure conditional health checks
    var healthChecksBuilder = builder.Services.AddHealthChecks()
        .AddCheck<WorkerHealthCheck>("worker");

    // Add health checks only for active dependencies
    if (!inMemorySettings.UseDatabase)
    {
        healthChecksBuilder.AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!);
    }

    if (!inMemorySettings.UseMessageBroker)
    {
        // TODO: Configure RabbitMQ health check when needed for production monitoring
        // For now, focusing on core in-memory functionality validation
        // healthChecksBuilder.AddRabbitMQ(...);
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
    if (builder.Environment.IsDevelopment() || 
        builder.Configuration.GetValue<bool>("HealthChecks:Enabled", false))
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Worker Service Configuration: {Configuration}", inMemorySettings.GetConfigurationSummary());
        logger.LogInformation("Environment: {Environment}", builder.Environment.EnvironmentName);
        
        if (inMemorySettings.HasInMemoryProviders)
        {
            logger.LogWarning("⚠️  In-memory providers enabled - suitable for development/testing only!");
        }
    }

    // Configure health check endpoints
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready")
    });
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false
    });

    // Map API endpoints
    app.MapControllers();
    app.MapOpenApi(); // .NET 9 native OpenAPI endpoint

    // Configure Prometheus metrics endpoint
    app.UseOpenTelemetryPrometheusScrapingEndpoint();

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