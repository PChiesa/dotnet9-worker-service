using Microsoft.EntityFrameworkCore;
using Npgsql;
using Polly;
using Polly.Extensions.Http;
using WorkerService.Infrastructure.Data;

namespace WorkerService.Worker.Services;

/// <summary>
/// Service for handling automatic database migrations with retry logic and proper error handling
/// </summary>
public class DatabaseMigrationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseMigrationService> _logger;
    private readonly IWebHostEnvironment _environment;

    public DatabaseMigrationService(
        IServiceProvider serviceProvider,
        ILogger<DatabaseMigrationService> logger,
        IWebHostEnvironment environment)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Applies pending database migrations with retry logic for production environments
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>True if migrations were applied successfully, false otherwise</returns>
    public async Task<bool> ApplyMigrationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Skip migrations for in-memory database
            if (context.Database.IsInMemory())
            {
                _logger.LogInformation("Using in-memory database - skipping migrations");
                await context.Database.EnsureCreatedAsync(cancellationToken);
                return true;
            }

            // Skip migrations in Test environment
            if (_environment.EnvironmentName == "Test")
            {
                _logger.LogInformation("Test environment detected - skipping migrations");
                return true;
            }

            _logger.LogInformation("Starting database migration process...");

            // Check for pending migrations
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);
            var pendingMigrationsList = pendingMigrations.ToList();

            if (!pendingMigrationsList.Any())
            {
                _logger.LogInformation("No pending migrations found - database is up to date");
                return true;
            }

            _logger.LogInformation("Found {Count} pending migrations: {Migrations}",
                pendingMigrationsList.Count,
                string.Join(", ", pendingMigrationsList));

            // Apply migrations with retry policy for production environments
            if (_environment.EnvironmentName == "Production")
            {
                await ApplyMigrationsWithRetryAsync(context, cancellationToken);
            }
            else
            {
                await ApplyMigrationsDirectlyAsync(context, cancellationToken);
            }

            // Verify migrations were applied successfully
            var remainingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);
            if (remainingMigrations.Any())
            {
                _logger.LogWarning("Some migrations may not have been applied successfully. Remaining: {Migrations}",
                    string.Join(", ", remainingMigrations));
                return false;
            }

            _logger.LogInformation("Database migrations completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply database migrations");
            return false;
        }
    }

    /// <summary>
    /// Applies migrations directly without retry logic (for development environments)
    /// </summary>
    private async Task ApplyMigrationsDirectlyAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Applying migrations directly (development mode)...");
        await context.Database.MigrateAsync(cancellationToken);
    }

    /// <summary>
    /// Applies migrations with retry logic for production environments
    /// </summary>
    private async Task ApplyMigrationsWithRetryAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        var retryPolicy = Policy
            .Handle<Exception>(ex => IsTransientException(ex))
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
                onRetry: (outcome, timespan, retryCount, policyContext) =>
                {
                    _logger.LogWarning("Migration attempt {RetryCount} failed, retrying in {Delay}ms. Error: {Error}",
                        retryCount,
                        timespan.TotalMilliseconds,
                        outcome.Message);
                });

        _logger.LogInformation("Applying migrations with retry policy (production mode)...");
        
        await retryPolicy.ExecuteAsync(async () =>
        {
            await context.Database.MigrateAsync(cancellationToken);
        });
    }

    /// <summary>
    /// Determines if an exception is transient and should be retried
    /// </summary>
    private static bool IsTransientException(Exception ex)
    {
        // Common transient exceptions for database operations
        return ex switch
        {
            TimeoutException => true,
            InvalidOperationException invalidOp when invalidOp.Message.Contains("timeout") => true,
            PostgresException pgEx when IsTransientPostgresError(pgEx) => true,
            _ when ex.Message.Contains("connection") => true,
            _ when ex.Message.Contains("network") => true,
            _ when ex.Message.Contains("timeout") => true,
            _ => false
        };
    }

    /// <summary>
    /// Determines if a PostgreSQL exception is transient
    /// </summary>
    private static bool IsTransientPostgresError(Exception ex)
    {
        // Check for transient PostgreSQL error codes
        var message = ex.Message.ToLowerInvariant();
        return message.Contains("connection") ||
               message.Contains("network") ||
               message.Contains("timeout") ||
               message.Contains("deadlock") ||
               message.Contains("lock") ||
               message.Contains("serialization failure");
    }

    /// <summary>
    /// Gets information about the current database state for logging
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation</param>
    /// <returns>Database state information</returns>
    public async Task<DatabaseStateInfo> GetDatabaseStateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var canConnect = await context.Database.CanConnectAsync(cancellationToken);
            var isInMemory = context.Database.IsInMemory();
            var appliedMigrations = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync(cancellationToken);

            return new DatabaseStateInfo
            {
                CanConnect = canConnect,
                IsInMemory = isInMemory,
                AppliedMigrations = appliedMigrations.ToList(),
                PendingMigrations = pendingMigrations.ToList()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get database state information");
            return new DatabaseStateInfo
            {
                CanConnect = false,
                IsInMemory = false,
                AppliedMigrations = new List<string>(),
                PendingMigrations = new List<string>(),
                Error = ex.Message
            };
        }
    }
}

/// <summary>
/// Information about the current database state
/// </summary>
public class DatabaseStateInfo
{
    public bool CanConnect { get; set; }
    public bool IsInMemory { get; set; }
    public List<string> AppliedMigrations { get; set; } = new();
    public List<string> PendingMigrations { get; set; } = new();
    public string? Error { get; set; }
}

