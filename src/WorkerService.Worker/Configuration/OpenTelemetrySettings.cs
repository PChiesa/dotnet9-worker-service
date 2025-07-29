namespace WorkerService.Worker.Configuration;

public class OpenTelemetrySettings
{
    public const string SectionName = "OpenTelemetry";
    
    public bool Enabled { get; set; } = true;
    public string ServiceName { get; set; } = "WorkerService";
    public OtlpSettings Otlp { get; set; } = new();
}

public class OtlpSettings
{
    public string Endpoint { get; set; } = string.Empty;
}