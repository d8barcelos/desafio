using OrderService.Application.Orders.Dtos;
using OrderService.Domain.Common;

namespace OrderService.Application.Orders;

public interface IOrderService
{
    Task<Result<OrderResponse>> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken);

    Task<Result<OrderResponse>> ConfirmAsync(Guid orderId, CancellationToken cancellationToken);

    Task<Result<OrderResponse>> CancelAsync(Guid orderId, CancellationToken cancellationToken);

    Task<Result<OrderResponse>> GetByIdAsync(Guid orderId, CancellationToken cancellationToken);

    Task<Result<PagedResult<OrderResponse>>> ListAsync(ListOrdersQuery query, CancellationToken cancellationToken);
}
