using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using OrderService.Application.Common.Abstractions;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.Auth;

internal sealed class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid? UserId => GetGuidClaim(ClaimTypes.NameIdentifier);

    public Guid? CustomerId => GetGuidClaim(JwtClaimTypes.CustomerId);

    public string? Role => Principal?.FindFirst(ClaimTypes.Role)?.Value;

    public bool IsAdmin => string.Equals(Role, Roles.Admin, StringComparison.Ordinal);

    public bool IsCustomer => string.Equals(Role, Roles.Customer, StringComparison.Ordinal);

    private Guid? GetGuidClaim(string claimType)
    {
        var value = Principal?.FindFirst(claimType)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
