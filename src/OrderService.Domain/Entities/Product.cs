using OrderService.Domain.Common;
using OrderService.Domain.Errors;

namespace OrderService.Domain.Entities;

public sealed class Product
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public decimal UnitPrice { get; private set; }
    public string Currency { get; private set; }
    public int AvailableQuantity { get; private set; }
    public uint Version { get; private set; }

    private Product()
    {
        Name = string.Empty;
        Currency = string.Empty;
    }

    public Product(Guid id, string name, decimal unitPrice, string currency, int availableQuantity)
    {
        Id = id;
        Name = name;
        UnitPrice = unitPrice;
        Currency = currency;
        AvailableQuantity = availableQuantity;
    }

    public Result DecrementStock(int quantity)
    {
        if (quantity <= 0)
        {
            return Result.Failure(DomainErrors.Product.InvalidQuantity);
        }

        if (AvailableQuantity < quantity)
        {
            return Result.Failure(DomainErrors.Product.InsufficientStock);
        }

        AvailableQuantity -= quantity;
        return Result.Success();
    }

    public void RestoreStock(int quantity)
    {
        if (quantity <= 0)
        {
            return;
        }

        AvailableQuantity += quantity;
    }
}
