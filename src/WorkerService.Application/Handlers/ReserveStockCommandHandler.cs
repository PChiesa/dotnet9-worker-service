using MediatR;
using Microsoft.Extensions.Logging;
using WorkerService.Application.Commands;
using WorkerService.Application.Common.DTOs;
using WorkerService.Application.Common.Extensions;
using WorkerService.Domain.Interfaces;

namespace WorkerService.Application.Handlers;

public class ReserveStockCommandHandler : IRequestHandler<ReserveStockCommand, ItemDto?>
{
    private readonly IItemRepository _repository;
    private readonly ILogger<ReserveStockCommandHandler> _logger;

    public ReserveStockCommandHandler(IItemRepository repository, ILogger<ReserveStockCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ItemDto?> Handle(ReserveStockCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reserving {Quantity} stock for item {ItemId} for order {OrderId}", 
            request.Quantity, request.ItemId, request.OrderId);

        var item = await _repository.GetByIdAsync(request.ItemId, cancellationToken);
        if (item == null)
        {
            _logger.LogWarning("Item {ItemId} not found", request.ItemId);
            return null;
        }

        try
        {
            item.ReserveStock(request.Quantity);

            await _repository.UpdateAsync(item, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex) when (ex.Message.Contains("concurrency") || ex.Message.Contains("conflict"))
        {
            _logger.LogWarning("Concurrent update detected for item {ItemId}", request.ItemId);
            throw new InvalidOperationException("The item was modified by another user. Please refresh and try again.");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Cannot reserve"))
        {
            _logger.LogWarning("Insufficient stock for item {ItemId}. Requested: {Quantity}, Available: {Available}", 
                request.ItemId, request.Quantity, item.StockLevel.Available);
            throw;
        }

        _logger.LogInformation("Stock reserved for item {ItemId}", request.ItemId);

        return item.ToDto();
    }
}