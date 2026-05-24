using OrderService.Domain.Entities;

namespace OrderService.Application.Common.Abstractions;

public interface IJwtTokenGenerator
{
    JwtToken Generate(User user);
}

public sealed record JwtToken(string Value, DateTimeOffset ExpiresAt);
