using System.Text.RegularExpressions;
using OrderService.Application.Common.Abstractions;
using OrderService.Application.Common.Errors;
using OrderService.Application.Common.Exceptions;
using OrderService.Application.Orders.Dtos;
using OrderService.Application.Orders.Mapping;
using OrderService.Domain.Common;
using OrderService.Domain.Entities;

namespace OrderService.Application.Orders;

public sealed class OrderApplicationService : IOrderService
{
    private const int MinPage = 1;
    private const int MinPageSize = 1;
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 20;

    private static readonly Regex CurrencyRegex = new("^[A-Z]{3}$", RegexOptions.Compiled);
    private static readonly Error ConflictRetry = ApplicationErrors.Conflict("Concurrent modification, please retry.");

    private readonly IOrderRepository _orders;
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUser _currentUser;

    public OrderApplicationService(
        IOrderRepository orders,
        IProductRepository products,
        IUnitOfWork uow,
        ICurrentUser currentUser)
    {
        _orders = orders;
        _products = products;
        _uow = uow;
        _currentUser = currentUser;
    }

    public async Task<Result<OrderResponse>> CreateAsync(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var validation = ValidateCreateRequest(request);
        if (!validation.IsSuccess)
        {
            return Result<OrderResponse>.Failure(validation.Error!);
        }

        var customerIdResult = ResolveCreateCustomerId(request.CustomerId);
        if (!customerIdResult.IsSuccess)
        {
            return Result<OrderResponse>.Failure(customerIdResult.Error!);
        }

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _products.GetByIdsAsync(productIds, cancellationToken);

        var productsCheck = ValidateProductsForCreate(products, request);
        if (!productsCheck.IsSuccess)
        {
            return Result<OrderResponse>.Failure(productsCheck.Error!);
        }

        var items = BuildNewOrderItems(products, request.Items);
        var orderResult = Order.Place(customerIdResult.Value, request.Currency, items);
        if (!orderResult.IsSuccess)
        {
            return Result<OrderResponse>.Failure(ApplicationErrors.Validation(orderResult.Error!.Message));
        }

        _orders.Add(orderResult.Value!);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result<OrderResponse>.Success(orderResult.Value!.ToResponse());
    }

    public async Task<Result<OrderResponse>> ConfirmAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null || !CanAccess(order))
        {
            return Result<OrderResponse>.Failure(ApplicationErrors.NotFound("Order"));
        }

        if (order.Status == OrderStatus.Confirmed)
        {
            return Result<OrderResponse>.Success(order.ToResponse());
        }

        var stockResult = await ApplyConfirmStockAsync(order, cancellationToken);
        if (!stockResult.IsSuccess)
        {
            return Result<OrderResponse>.Failure(stockResult.Error!);
        }

        var transition = order.Confirm();
        if (!transition.IsSuccess)
        {
            return Result<OrderResponse>.Failure(ApplicationErrors.Conflict(transition.Error!.Message));
        }

        return await SaveAndReturnAsync(order, cancellationToken);
    }

    public async Task<Result<OrderResponse>> CancelAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdAsync(orderId, cancellationToken);
        if (order is null || !CanAccess(order))
        {
            return Result<OrderResponse>.Failure(ApplicationErrors.NotFound("Order"));
        }

        if (order.Status == OrderStatus.Canceled)
        {
            return Result<OrderResponse>.Success(order.ToResponse());
        }

        var wasConfirmed = order.Status == OrderStatus.Confirmed;
        var transition = order.Cancel();
        if (!transition.IsSuccess)
        {
            return Result<OrderResponse>.Failure(ApplicationErrors.Conflict(transition.Error!.Message));
        }

        if (wasConfirmed)
        {
            await RestoreStockAsync(order, cancellationToken);
        }

        return await SaveAndReturnAsync(order, cancellationToken);
    }

    public async Task<Result<OrderResponse>> GetByIdAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdReadOnlyAsync(orderId, cancellationToken);
        if (order is null || !CanAccess(order))
        {
            return Result<OrderResponse>.Failure(ApplicationErrors.NotFound("Order"));
        }

        return Result<OrderResponse>.Success(order.ToResponse());
    }

    public async Task<Result<PagedResult<OrderResponse>>> ListAsync(ListOrdersQuery query, CancellationToken cancellationToken)
    {
        (int page, int pageSize) = ClampPaging(query.Page, query.PageSize);
        var customerFilter = ResolveListCustomerFilter(query.CustomerId);
        if (!customerFilter.IsSuccess)
        {
            return Result<PagedResult<OrderResponse>>.Failure(customerFilter.Error!);
        }

        var (items, total) = await _orders.ListAsync(
            customerFilter.Value,
            query.Status,
            query.From,
            query.To,
            page,
            pageSize,
            cancellationToken);

        var responses = items.Select(o => o.ToResponse()).ToList();
        return Result<PagedResult<OrderResponse>>.Success(new PagedResult<OrderResponse>(responses, page, pageSize, total));
    }

    private static Result ValidateCreateRequest(CreateOrderRequest request)
    {
        if (request is null)
        {
            return Result.Failure(ApplicationErrors.Validation("Request body is required."));
        }

        if (string.IsNullOrWhiteSpace(request.Currency) || !CurrencyRegex.IsMatch(request.Currency))
        {
            return Result.Failure(ApplicationErrors.Validation("Currency must be a 3-letter uppercase ISO 4217 code."));
        }

        if (request.Items is null || request.Items.Count == 0)
        {
            return Result.Failure(ApplicationErrors.Validation("At least one item is required."));
        }

        if (request.Items.Any(i => i.Quantity <= 0))
        {
            return Result.Failure(ApplicationErrors.Validation("Item quantity must be greater than zero."));
        }

        if (request.Items.Any(i => i.ProductId == Guid.Empty))
        {
            return Result.Failure(ApplicationErrors.Validation("ProductId is required for every item."));
        }

        return Result.Success();
    }

    private Result<Guid> ResolveCreateCustomerId(Guid? requested)
    {
        if (_currentUser.IsAdmin)
        {
            if (requested is null || requested.Value == Guid.Empty)
            {
                return Result<Guid>.Failure(ApplicationErrors.Validation("CustomerId is required when creating an order as Admin."));
            }
            return Result<Guid>.Success(requested.Value);
        }

        if (!_currentUser.IsCustomer || _currentUser.CustomerId is null)
        {
            return Result<Guid>.Failure(ApplicationErrors.Forbidden());
        }

        if (requested is not null && requested.Value != _currentUser.CustomerId.Value)
        {
            return Result<Guid>.Failure(ApplicationErrors.Forbidden());
        }

        return Result<Guid>.Success(_currentUser.CustomerId.Value);
    }

    private static Result ValidateProductsForCreate(IReadOnlyList<Product> products, CreateOrderRequest request)
    {
        var productsById = products.ToDictionary(p => p.Id);
        foreach (var item in request.Items)
        {
            if (!productsById.TryGetValue(item.ProductId, out var product))
            {
                return Result.Failure(ApplicationErrors.NotFound("Product"));
            }

            if (!string.Equals(product.Currency, request.Currency, StringComparison.Ordinal))
            {
                return Result.Failure(ApplicationErrors.Validation(
                    $"Product currency '{product.Currency}' does not match order currency '{request.Currency}'."));
            }

            if (product.AvailableQuantity < item.Quantity)
            {
                return Result.Failure(ApplicationErrors.Conflict(
                    $"Insufficient stock for product {product.Id}. Available: {product.AvailableQuantity}, requested: {item.Quantity}."));
            }
        }

        return Result.Success();
    }

    private static List<NewOrderItem> BuildNewOrderItems(IReadOnlyList<Product> products, IReadOnlyList<CreateOrderItemRequest> items)
    {
        var productsById = products.ToDictionary(p => p.Id);
        return items.Select(i => new NewOrderItem(i.ProductId, productsById[i.ProductId].UnitPrice, i.Quantity)).ToList();
    }

    private async Task<Result> ApplyConfirmStockAsync(Order order, CancellationToken cancellationToken)
    {
        var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _products.GetByIdsAsync(productIds, cancellationToken);
        var productsById = products.ToDictionary(p => p.Id);

        foreach (var item in order.Items)
        {
            if (!productsById.TryGetValue(item.ProductId, out var product))
            {
                return Result.Failure(ApplicationErrors.NotFound("Product"));
            }

            var decrement = product.DecrementStock(item.Quantity);
            if (!decrement.IsSuccess)
            {
                return Result.Failure(ApplicationErrors.Conflict(decrement.Error!.Message));
            }
        }

        return Result.Success();
    }

    private async Task RestoreStockAsync(Order order, CancellationToken cancellationToken)
    {
        var productIds = order.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _products.GetByIdsAsync(productIds, cancellationToken);
        var productsById = products.ToDictionary(p => p.Id);

        foreach (var item in order.Items)
        {
            if (productsById.TryGetValue(item.ProductId, out var product))
            {
                product.RestoreStock(item.Quantity);
            }
        }
    }

    private async Task<Result<OrderResponse>> SaveAndReturnAsync(Order order, CancellationToken cancellationToken)
    {
        try
        {
            await _uow.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            return Result<OrderResponse>.Failure(ConflictRetry);
        }

        return Result<OrderResponse>.Success(order.ToResponse());
    }

    private bool CanAccess(Order order)
    {
        if (_currentUser.IsAdmin)
        {
            return true;
        }

        return _currentUser.IsCustomer && _currentUser.CustomerId == order.CustomerId;
    }

    private Result<Guid?> ResolveListCustomerFilter(Guid? requested)
    {
        if (_currentUser.IsAdmin)
        {
            return Result<Guid?>.Success(requested);
        }

        if (!_currentUser.IsCustomer || _currentUser.CustomerId is null)
        {
            return Result<Guid?>.Failure(ApplicationErrors.Forbidden());
        }

        if (requested is not null && requested.Value != _currentUser.CustomerId.Value)
        {
            return Result<Guid?>.Failure(ApplicationErrors.Forbidden());
        }

        return Result<Guid?>.Success(_currentUser.CustomerId);
    }

    private static (int Page, int PageSize) ClampPaging(int page, int pageSize)
    {
        var clampedPage = page < MinPage ? MinPage : page;
        var clampedPageSize = pageSize <= 0
            ? DefaultPageSize
            : pageSize < MinPageSize ? MinPageSize : pageSize > MaxPageSize ? MaxPageSize : pageSize;
        return (clampedPage, clampedPageSize);
    }
}
