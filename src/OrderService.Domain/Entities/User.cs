namespace OrderService.Domain.Entities;

public sealed class User
{
    public Guid Id { get; private set; }
    public string Username { get; private set; }
    public string PasswordHash { get; private set; }
    public string Role { get; private set; }
    public Guid? CustomerId { get; private set; }

    private User()
    {
        Username = string.Empty;
        PasswordHash = string.Empty;
        Role = string.Empty;
    }

    public User(Guid id, string username, string passwordHash, string role, Guid? customerId)
    {
        Id = id;
        Username = username;
        PasswordHash = passwordHash;
        Role = role;
        CustomerId = customerId;
    }

    public bool IsAdmin => Role == Roles.Admin;
    public bool IsCustomer => Role == Roles.Customer;
}
