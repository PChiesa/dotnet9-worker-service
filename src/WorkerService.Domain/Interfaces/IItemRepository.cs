using WorkerService.Domain.Entities;

namespace WorkerService.Domain.Interfaces;

public interface IItemRepository : IRepository<Item>
{
    Task<Item?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default);
    Task<bool> SkuExistsAsync(string sku, CancellationToken cancellationToken = default);
    Task<(IEnumerable<Item> Items, int TotalCount)> GetPagedAsync(
        int pageNumber, 
        int pageSize, 
        string? category = null,
        bool? isActive = null,
        string? searchTerm = null,
        CancellationToken cancellationToken = default);
}