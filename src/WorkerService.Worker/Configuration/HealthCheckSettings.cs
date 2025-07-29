namespace WorkerService.Worker.Configuration;

public class HealthCheckSettings
{
    public const string SectionName = "HealthChecks";
    
    public bool Enabled { get; set; } = true;
}