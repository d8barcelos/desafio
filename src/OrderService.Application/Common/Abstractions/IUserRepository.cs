using OrderService.Domain.Entities;

namespace OrderService.Application.Common.Abstractions;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken);
}
