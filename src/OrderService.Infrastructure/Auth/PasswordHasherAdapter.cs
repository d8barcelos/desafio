using Microsoft.AspNetCore.Identity;
using OrderService.Application.Common.Abstractions;

namespace OrderService.Infrastructure.Auth;

internal sealed class PasswordHasherAdapter : IPasswordHasher
{
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object HashContext = new();

    public string Hash(string password) => Hasher.HashPassword(HashContext, password);

    public bool Verify(string password, string hash)
    {
        var result = Hasher.VerifyHashedPassword(HashContext, hash, password);
        return result is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
