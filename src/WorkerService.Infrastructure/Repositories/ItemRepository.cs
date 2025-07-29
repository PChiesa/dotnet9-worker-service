using Microsoft.EntityFrameworkCore;
using WorkerService.Domain.Entities;
using WorkerService.Domain.Interfaces;
using WorkerService.Infrastructure.Data;

namespace WorkerService.Infrastructure.Repositories;

public class ItemRepository : Repository<Item>, IItemRepository
{
    private readonly ApplicationDbContext _context;

    public ItemRepository(ApplicationDbContext context) : base(context)
    {
        _context = context;
    }

    public async Task<Item?> GetBySkuAsync(string sku, CancellationToken cancellationToken = default)
    {
        return await _context.Items
            .FirstOrDefaultAsync(i => i.SKU.Value == sku, cancellationToken);
    }

    public async Task<bool> SkuExistsAsync(string sku, CancellationToken cancellationToken = default)
    {
        return await _context.Items
            .AnyAsync(i => i.SKU.Value == sku, cancellationToken);
    }

    public async Task<(IEnumerable<Item> Items, int TotalCount)> GetPagedAsync(
        int pageNumber, 
        int pageSize, 
        string? category = null,
        bool? isActive = null,
        string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Items.AsQueryable();

        // Apply filters
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(i => i.Category == category);
        }

        if (isActive.HasValue)
        {
            query = query.Where(i => i.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var searchLower = searchTerm.ToLower();
            query = query.Where(i => 
                i.Name.ToLower().Contains(searchLower) || 
                i.SKU.Value.ToLower().Contains(searchLower) ||
                i.Description.ToLower().Contains(searchLower));
        }

        // Get total count
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination
        var items = await query
            .OrderBy(i => i.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}