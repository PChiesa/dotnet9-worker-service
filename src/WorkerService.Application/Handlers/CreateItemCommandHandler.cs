using MediatR;
using Microsoft.Extensions.Logging;
using WorkerService.Application.Commands;
using WorkerService.Application.Common.DTOs;
using WorkerService.Application.Common.Extensions;
using WorkerService.Application.Common.Metrics;
using WorkerService.Domain.Entities;
using WorkerService.Domain.Interfaces;
using WorkerService.Domain.ValueObjects;

namespace WorkerService.Application.Handlers;

public class CreateItemCommandHandler : IRequestHandler<CreateItemCommand, ItemDto>
{
    private readonly IItemRepository _repository;
    private readonly ILogger<CreateItemCommandHandler> _logger;

    public CreateItemCommandHandler(IItemRepository repository, ILogger<CreateItemCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ItemDto> Handle(CreateItemCommand request, CancellationToken cancellationToken)
    {
        using var activity = ItemApiMetrics.ActivitySource.StartActivity("CreateItem");
        activity?.SetTag("item.sku", request.SKU);
        activity?.SetTag("item.category", request.Category);
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation("Creating item with SKU {SKU}", request.SKU);

            // Check if SKU already exists
            if (await _repository.SkuExistsAsync(request.SKU, cancellationToken))
            {
                throw new InvalidOperationException($"Item with SKU '{request.SKU}' already exists");
            }

            var sku = new SKU(request.SKU);
            var price = new Price(request.Price);
            
            var item = new Item(
                sku,
                request.Name,
                request.Description,
                price,
                request.InitialStock,
                request.Category
            );

            await _repository.AddAsync(item, cancellationToken);
            await _repository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Item {ItemId} with SKU {SKU} created successfully", item.Id, request.SKU);

            // Record metrics
            ItemApiMetrics.ItemsCreated.Add(1, 
                new KeyValuePair<string, object?>("sku", request.SKU),
                new KeyValuePair<string, object?>("category", request.Category));
            ItemApiMetrics.ActiveItems.Add(1);

            return item.ToDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create item with SKU {SKU}", request.SKU);
            throw;
        }
        finally
        {
            stopwatch.Stop();
            ItemApiMetrics.ItemCreationDuration.Record(stopwatch.ElapsedMilliseconds);
        }
    }
}