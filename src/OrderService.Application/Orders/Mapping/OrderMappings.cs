using OrderService.Application.Orders.Dtos;
using OrderService.Domain.Entities;

namespace OrderService.Application.Orders.Mapping;

public static class OrderMappings
{
    public static OrderResponse ToResponse(this Order order) => new(
        order.Id,
        order.CustomerId,
        order.Status.ToString(),
        order.Currency,
        order.Total,
        order.CreatedAt,
        order.Items.Select(ToResponse).ToList());

    public static OrderItemResponse ToResponse(this OrderItem item) => new(
        item.Id,
        item.ProductId,
        item.UnitPrice,
        item.Quantity,
        item.LineTotal);
}
