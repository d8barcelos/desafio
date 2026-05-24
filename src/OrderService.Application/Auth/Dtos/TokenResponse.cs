namespace OrderService.Application.Auth.Dtos;

public sealed record TokenResponse(string Token, DateTimeOffset ExpiresAt);
