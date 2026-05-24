using OrderService.Domain.Entities;

namespace OrderService.Application.Common.Abstractions;

public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
}
