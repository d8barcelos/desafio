using OrderService.Domain.Common;
using OrderService.Domain.Errors;

namespace OrderService.Domain.Entities;

public sealed class Order
{
    private readonly List<OrderItem> _items;

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public string Currency { get; private set; }
    public decimal Total { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public uint Version { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    private Order()
    {
        _items = new List<OrderItem>();
        Currency = string.Empty;
    }

    private Order(Guid id, Guid customerId, string currency, DateTimeOffset createdAt, List<OrderItem> items)
    {
        Id = id;
        CustomerId = customerId;
        Currency = currency;
        CreatedAt = createdAt;
        _items = items;
        Status = OrderStatus.Placed;
        Total = items.Sum(i => i.LineTotal);
    }

    public static Result<Order> Place(Guid customerId, string currency, IReadOnlyCollection<NewOrderItem> items)
    {
        if (items is null || items.Count == 0)
        {
            return Result<Order>.Failure(DomainErrors.Order.NoItems);
        }

        if (items.Any(i => i.Quantity <= 0))
        {
            return Result<Order>.Failure(DomainErrors.Order.InvalidQuantity);
        }

        var orderId = Guid.NewGuid();
        var orderItems = items
            .Select(d => new OrderItem(Guid.NewGuid(), orderId, d.ProductId, d.UnitPrice, d.Quantity))
            .ToList();

        var order = new Order(orderId, customerId, currency, DateTimeOffset.UtcNow, orderItems);
        return Result<Order>.Success(order);
    }

    public Result Confirm()
    {
        if (Status == OrderStatus.Confirmed)
        {
            return Result.Success();
        }

        if (Status != OrderStatus.Placed)
        {
            return Result.Failure(DomainErrors.Order.InvalidTransition);
        }

        Status = OrderStatus.Confirmed;
        return Result.Success();
    }

    public Result Cancel()
    {
        if (Status == OrderStatus.Canceled)
        {
            return Result.Success();
        }

        Status = OrderStatus.Canceled;
        return Result.Success();
    }
}
