using Microsoft.EntityFrameworkCore;
using OrderService.Application.Common.Abstractions;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository : IUserRepository
{
    private readonly OrderDbContext _db;

    public UserRepository(OrderDbContext db)
    {
        _db = db;
    }

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken) =>
        _db.Users.FirstOrDefaultAsync(u => u.Username == username, cancellationToken);
}
