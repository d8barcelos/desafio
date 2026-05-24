using OrderService.Domain.Entities;

namespace OrderService.Application.Orders.Dtos;

public sealed record ListOrdersQuery(
    Guid? CustomerId,
    OrderStatus? Status,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page,
    int PageSize);
