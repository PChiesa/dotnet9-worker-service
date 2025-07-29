using MediatR;
using Microsoft.Extensions.Logging;
using WorkerService.Application.Common.DTOs;
using WorkerService.Application.Common.Extensions;
using WorkerService.Application.Queries;
using WorkerService.Domain.Interfaces;

namespace WorkerService.Application.Handlers;

public class GetItemBySkuQueryHandler : IRequestHandler<GetItemBySkuQuery, ItemDto?>
{
    private readonly IItemRepository _repository;
    private readonly ILogger<GetItemBySkuQueryHandler> _logger;

    public GetItemBySkuQueryHandler(IItemRepository repository, ILogger<GetItemBySkuQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ItemDto?> Handle(GetItemBySkuQuery request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Getting item by SKU {SKU}", request.SKU);

        var item = await _repository.GetBySkuAsync(request.SKU, cancellationToken);
        
        return item?.ToDto();
    }
}