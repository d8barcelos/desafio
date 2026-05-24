using OrderService.Application.Auth.Dtos;
using OrderService.Domain.Common;

namespace OrderService.Application.Auth;

public interface IAuthService
{
    Task<Result<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}
