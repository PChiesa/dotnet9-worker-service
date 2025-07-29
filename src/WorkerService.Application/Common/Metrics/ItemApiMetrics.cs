using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace WorkerService.Application.Common.Metrics;

/// <summary>
/// Custom metrics and activity source for Item API operations
/// </summary>
public static class ItemApiMetrics
{
    private static readonly Meter _meter = new("ItemsAPI", "1.0.0");
    private static readonly ActivitySource _activitySource = new("ItemsAPI", "1.0.0");
    
    public static ActivitySource ActivitySource => _activitySource;
    
    // Counters for API operations
    public static readonly Counter<int> ItemsCreated = _meter.CreateCounter<int>(
        "items.created.total", 
        description: "Total number of items created via API");
        
    public static readonly Counter<int> ItemsUpdated = _meter.CreateCounter<int>(
        "items.updated.total", 
        description: "Total number of items updated via API");
        
    public static readonly Counter<int> ItemsDeactivated = _meter.CreateCounter<int>(
        "items.deactivated.total", 
        description: "Total number of items deactivated via API");
        
    public static readonly Counter<int> ItemsRetrieved = _meter.CreateCounter<int>(
        "items.retrieved.total", 
        description: "Total number of items retrieved via API");
    
    public static readonly Counter<int> StockAdjustments = _meter.CreateCounter<int>(
        "items.stock.adjustments.total", 
        description: "Total number of stock adjustments");
        
    public static readonly Counter<int> StockReservations = _meter.CreateCounter<int>(
        "items.stock.reservations.total", 
        description: "Total number of stock reservations");
    
    // Histograms for performance tracking
    public static readonly Histogram<double> ItemCreationDuration = _meter.CreateHistogram<double>(
        "item.creation.duration", 
        unit: "ms", 
        description: "Duration of item creation operations");
        
    public static readonly Histogram<double> ItemUpdateDuration = _meter.CreateHistogram<double>(
        "item.update.duration", 
        unit: "ms", 
        description: "Duration of item update operations");
        
    public static readonly Histogram<double> ItemQueryDuration = _meter.CreateHistogram<double>(
        "item.query.duration", 
        unit: "ms", 
        description: "Duration of item query operations");
        
    public static readonly Histogram<double> StockOperationDuration = _meter.CreateHistogram<double>(
        "item.stock.operation.duration", 
        unit: "ms", 
        description: "Duration of stock operations (adjust, reserve, etc.)");
    
    // Gauges for current state
    public static readonly UpDownCounter<int> ActiveItems = _meter.CreateUpDownCounter<int>(
        "items.active.count", 
        description: "Current number of active items");
        
    public static readonly Histogram<int> ItemsPerPage = _meter.CreateHistogram<int>(
        "items.per.page", 
        description: "Number of items returned per page in queries");
}