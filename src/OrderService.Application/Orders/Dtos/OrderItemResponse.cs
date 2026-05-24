namespace OrderService.Application.Orders.Dtos;

public sealed record OrderItemResponse(
    Guid Id,
    Guid ProductId,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal);
