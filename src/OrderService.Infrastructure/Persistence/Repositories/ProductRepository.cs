using Microsoft.EntityFrameworkCore;
using OrderService.Application.Common.Abstractions;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.Persistence.Repositories;

internal sealed class ProductRepository : IProductRepository
{
    private readonly OrderDbContext _db;

    public ProductRepository(OrderDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return Array.Empty<Product>();
        }

        return await _db.Products
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(cancellationToken);
    }
}
