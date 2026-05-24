using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Application.Common.Abstractions;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    private static readonly Guid SeedCustomerEntityId = new("11111111-1111-1111-1111-111111111111");

    public static async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var db = services.GetRequiredService<OrderDbContext>();
        var hasher = services.GetRequiredService<IPasswordHasher>();

        await SeedUsersAsync(db, hasher, cancellationToken);
        await SeedProductsAsync(db, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedUsersAsync(OrderDbContext db, IPasswordHasher hasher, CancellationToken cancellationToken)
    {
        if (await db.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        db.Users.Add(new User(
            id: new Guid("00000000-0000-0000-0000-000000000001"),
            username: "admin",
            passwordHash: hasher.Hash("Admin@123"),
            role: Roles.Admin,
            customerId: null));

        db.Users.Add(new User(
            id: new Guid("00000000-0000-0000-0000-000000000002"),
            username: "customer",
            passwordHash: hasher.Hash("Customer@123"),
            role: Roles.Customer,
            customerId: SeedCustomerEntityId));
    }

    private static async Task SeedProductsAsync(OrderDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Products.AnyAsync(cancellationToken))
        {
            return;
        }

        var products = new[]
        {
            new Product(new Guid("aaaaaaaa-0000-0000-0000-000000000001"), "Coffee Mug", 9.99m, "USD", 100),
            new Product(new Guid("aaaaaaaa-0000-0000-0000-000000000002"), "T-Shirt", 19.50m, "USD", 50),
            new Product(new Guid("aaaaaaaa-0000-0000-0000-000000000003"), "Notebook", 4.25m, "USD", 200),
            new Product(new Guid("aaaaaaaa-0000-0000-0000-000000000004"), "Wireless Mouse", 24.90m, "USD", 30),
            new Product(new Guid("aaaaaaaa-0000-0000-0000-000000000005"), "Mechanical Keyboard", 89.00m, "USD", 15),
        };

        db.Products.AddRange(products);
    }
}
