using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace WorkerService.Application.Common.Metrics;

/// <summary>
/// Custom metrics and activity source for Order API operations
/// </summary>
public static class OrderApiMetrics
{
    private static readonly Meter _meter = new("OrdersAPI", "1.0.0");
    private static readonly ActivitySource _activitySource = new("OrdersAPI", "1.0.0");
    
    public static ActivitySource ActivitySource => _activitySource;
    
    // Counters for API operations
    public static readonly Counter<int> OrdersCreated = _meter.CreateCounter<int>(
        "orders.created.total", 
        description: "Total number of orders created via API");
        
    public static readonly Counter<int> OrdersUpdated = _meter.CreateCounter<int>(
        "orders.updated.total", 
        description: "Total number of orders updated via API");
        
    public static readonly Counter<int> OrdersDeleted = _meter.CreateCounter<int>(
        "orders.deleted.total", 
        description: "Total number of orders deleted via API");
        
    public static readonly Counter<int> OrdersRetrieved = _meter.CreateCounter<int>(
        "orders.retrieved.total", 
        description: "Total number of orders retrieved via API");
    
    // Histograms for performance tracking
    public static readonly Histogram<double> OrderCreationDuration = _meter.CreateHistogram<double>(
        "order.creation.duration", 
        unit: "ms", 
        description: "Duration of order creation operations");
        
    public static readonly Histogram<double> OrderUpdateDuration = _meter.CreateHistogram<double>(
        "order.update.duration", 
        unit: "ms", 
        description: "Duration of order update operations");
        
    public static readonly Histogram<double> OrderQueryDuration = _meter.CreateHistogram<double>(
        "order.query.duration", 
        unit: "ms", 
        description: "Duration of order query operations");
}