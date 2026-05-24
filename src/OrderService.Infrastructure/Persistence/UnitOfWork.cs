using Microsoft.EntityFrameworkCore;
using OrderService.Application.Common.Abstractions;
using OrderService.Application.Common.Exceptions;

namespace OrderService.Infrastructure.Persistence;

internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly OrderDbContext _db;

    public UnitOfWork(OrderDbContext db)
    {
        _db = db;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("Concurrent modification detected.", ex);
        }
    }
}
