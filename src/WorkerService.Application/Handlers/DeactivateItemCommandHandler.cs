using MediatR;
using Microsoft.Extensions.Logging;
using WorkerService.Application.Commands;
using WorkerService.Domain.Interfaces;

namespace WorkerService.Application.Handlers;

public class DeactivateItemCommandHandler : IRequestHandler<DeactivateItemCommand, bool>
{
    private readonly IItemRepository _repository;
    private readonly ILogger<DeactivateItemCommandHandler> _logger;

    public DeactivateItemCommandHandler(IItemRepository repository, ILogger<DeactivateItemCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<bool> Handle(DeactivateItemCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Deactivating item {ItemId}", request.ItemId);

        var item = await _repository.GetByIdAsync(request.ItemId, cancellationToken);
        if (item == null)
        {
            _logger.LogWarning("Item {ItemId} not found", request.ItemId);
            return false;
        }

        item.Deactivate();

        await _repository.UpdateAsync(item, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Item {ItemId} deactivated successfully", request.ItemId);

        return true;
    }
}