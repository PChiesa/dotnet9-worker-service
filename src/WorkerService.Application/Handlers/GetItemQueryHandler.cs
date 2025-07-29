using MediatR;
using Microsoft.Extensions.Logging;
using WorkerService.Application.Common.DTOs;
using WorkerService.Application.Common.Extensions;
using WorkerService.Application.Queries;
using WorkerService.Domain.Interfaces;

namespace WorkerService.Application.Handlers;

public class GetItemQueryHandler : IRequestHandler<GetItemQuery, ItemDto?>
{
    private readonly IItemRepository _repository;
    private readonly ILogger<GetItemQueryHandler> _logger;

    public GetItemQueryHandler(IItemRepository repository, ILogger<GetItemQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ItemDto?> Handle(GetItemQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting item {ItemId}", request.ItemId);

        var item = await _repository.GetByIdAsync(request.ItemId, cancellationToken);
        
        return item?.ToDto();
    }
}