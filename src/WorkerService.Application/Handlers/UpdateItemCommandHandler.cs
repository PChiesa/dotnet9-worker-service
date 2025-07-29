using MediatR;
using Microsoft.Extensions.Logging;
using WorkerService.Application.Commands;
using WorkerService.Application.Common.DTOs;
using WorkerService.Application.Common.Extensions;
using WorkerService.Domain.Interfaces;
using WorkerService.Domain.ValueObjects;

namespace WorkerService.Application.Handlers;

public class UpdateItemCommandHandler : IRequestHandler<UpdateItemCommand, ItemDto?>
{
    private readonly IItemRepository _repository;
    private readonly ILogger<UpdateItemCommandHandler> _logger;

    public UpdateItemCommandHandler(IItemRepository repository, ILogger<UpdateItemCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ItemDto?> Handle(UpdateItemCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Updating item {ItemId}", request.ItemId);

        var item = await _repository.GetByIdAsync(request.ItemId, cancellationToken);
        if (item == null)
        {
            _logger.LogWarning("Item {ItemId} not found", request.ItemId);
            return null;
        }

        var price = new Price(request.Price);
        item.Update(request.Name, request.Description, price, request.Category);

        await _repository.UpdateAsync(item, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Item {ItemId} updated successfully", request.ItemId);

        return item.ToDto();
    }
}