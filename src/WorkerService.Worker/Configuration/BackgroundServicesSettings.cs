namespace WorkerService.Worker.Configuration;

public class BackgroundServicesSettings
{
    public const string SectionName = "BackgroundServices";
    
    public ServiceSettings OrderSimulator { get; set; } = new();
    public ServiceSettings MetricsCollection { get; set; } = new();
    public ServiceSettings OrderProcessing { get; set; } = new();
}

public class ServiceSettings
{
    public bool Enabled { get; set; } = true;
    public int IntervalMs { get; set; } = 1000;
    public int MaxOrders { get; set; } = 100;
}