namespace OrderService.Domain.Entities;

public sealed record NewOrderItem(Guid ProductId, decimal UnitPrice, int Quantity);
