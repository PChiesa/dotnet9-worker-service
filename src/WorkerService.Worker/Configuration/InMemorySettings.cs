namespace WorkerService.Worker.Configuration;

/// <summary>
/// Configuration settings for enabling in-memory dependencies.
/// Used for local development and testing scenarios.
/// </summary>
public class InMemorySettings
{
    public const string SectionName = "InMemory";

    /// <summary>
    /// When true, uses Entity Framework Core in-memory database instead of PostgreSQL.
    /// Default: false (uses PostgreSQL)
    /// </summary>
    public bool UseDatabase { get; set; } = false;

    /// <summary>
    /// When true, uses MassTransit in-memory transport instead of RabbitMQ.
    /// Default: false (uses RabbitMQ)
    /// </summary>
    public bool UseMessageBroker { get; set; } = false;

    /// <summary>
    /// Validates the configuration settings.
    /// </summary>
    public bool IsValid => true; // All combinations are valid

    /// <summary>
    /// Returns true if any in-memory provider is enabled.
    /// </summary>
    public bool HasInMemoryProviders => UseDatabase || UseMessageBroker;

    /// <summary>
    /// Returns a summary of the current configuration for logging purposes.
    /// </summary>
    public string GetConfigurationSummary()
    {
        return $"Database: {(UseDatabase ? "In-Memory" : "PostgreSQL")}, MessageBroker: {(UseMessageBroker ? "In-Memory" : "RabbitMQ")}";
    }
}