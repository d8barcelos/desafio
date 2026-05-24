namespace OrderService.Application.Orders.Dtos;

public sealed record CreateOrderRequest(
    Guid? CustomerId,
    string Currency,
    IReadOnlyList<CreateOrderItemRequest> Items);

public sealed record CreateOrderItemRequest(Guid ProductId, int Quantity);
