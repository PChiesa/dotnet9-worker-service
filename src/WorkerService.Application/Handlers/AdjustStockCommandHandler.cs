using MediatR;
using Microsoft.Extensions.Logging;
using WorkerService.Application.Commands;
using WorkerService.Application.Common.DTOs;
using WorkerService.Application.Common.Extensions;
using WorkerService.Application.Common.Metrics;
using WorkerService.Domain.Interfaces;

namespace WorkerService.Application.Handlers;

public class AdjustStockCommandHandler : IRequestHandler<AdjustStockCommand, ItemDto?>
{
    private readonly IItemRepository _repository;
    private readonly ILogger<AdjustStockCommandHandler> _logger;

    public AdjustStockCommandHandler(IItemRepository repository, ILogger<AdjustStockCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ItemDto?> Handle(AdjustStockCommand request, CancellationToken cancellationToken)
    {
        using var activity = ItemApiMetrics.ActivitySource.StartActivity("AdjustStock");
        activity?.SetTag("item.id", request.ItemId.ToString());
        activity?.SetTag("stock.new_quantity", request.NewQuantity);
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Adjusting stock for item {ItemId} to {Quantity}", 
                request.ItemId, request.NewQuantity);

            var item = await _repository.GetByIdAsync(request.ItemId, cancellationToken);
            if (item == null)
            {
                _logger.LogWarning("Item {ItemId} not found", request.ItemId);
                return null;
            }

            var oldQuantity = item.StockLevel.Available;
            item.AdjustStock(request.NewQuantity);

            try
            {
                await _repository.UpdateAsync(item, cancellationToken);
                await _repository.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex) when (ex.Message.Contains("concurrency") || ex.Message.Contains("conflict"))
            {
                _logger.LogWarning("Concurrent update detected for item {ItemId}", request.ItemId);
                throw new InvalidOperationException("The item was modified by another user. Please refresh and try again.");
            }

            _logger.LogInformation("Stock adjusted for item {ItemId}", request.ItemId);

            // Record metrics
            ItemApiMetrics.StockAdjustments.Add(1, 
                new KeyValuePair<string, object?>("item.id", request.ItemId.ToString()),
                new KeyValuePair<string, object?>("old_quantity", oldQuantity),
                new KeyValuePair<string, object?>("new_quantity", request.NewQuantity));

            return item.ToDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to adjust stock for item {ItemId}", request.ItemId);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            ItemApiMetrics.StockOperationDuration.Record(stopwatch.ElapsedMilliseconds);
        }
    }
}