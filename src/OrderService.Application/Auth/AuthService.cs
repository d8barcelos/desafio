using OrderService.Application.Auth.Dtos;
using OrderService.Application.Common.Abstractions;
using OrderService.Application.Common.Errors;
using OrderService.Domain.Common;

namespace OrderService.Application.Auth;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenGenerator _tokens;

    public AuthService(IUserRepository users, IPasswordHasher hasher, IJwtTokenGenerator tokens)
    {
        _users = users;
        _hasher = hasher;
        _tokens = tokens;
    }

    public async Task<Result<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Result<TokenResponse>.Failure(ApplicationErrors.InvalidCredentials());
        }

        var user = await _users.GetByUsernameAsync(request.Username, cancellationToken);
        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
        {
            return Result<TokenResponse>.Failure(ApplicationErrors.InvalidCredentials());
        }

        var token = _tokens.Generate(user);
        return Result<TokenResponse>.Success(new TokenResponse(token.Value, token.ExpiresAt));
    }
}
