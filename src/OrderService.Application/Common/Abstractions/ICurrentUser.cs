namespace OrderService.Application.Common.Abstractions;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    Guid? CustomerId { get; }

    string? Role { get; }

    bool IsAdmin { get; }

    bool IsCustomer { get; }
}
