using FluentAssertions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using OrderService.Application.Common.Abstractions;
using OrderService.Application.Common.Exceptions;
using OrderService.Application.Orders;
using OrderService.Application.Orders.Dtos;
using OrderService.Domain.Entities;

namespace OrderService.Application.Tests.Orders;

public class OrderServiceTests
{
    private readonly IOrderRepository _orders = Substitute.For<IOrderRepository>();
    private readonly IProductRepository _products = Substitute.For<IProductRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();

    private static readonly Guid AdminUserId = Guid.NewGuid();
    private static readonly Guid CustomerUserId = Guid.NewGuid();
    private static readonly Guid CustomerEntityId = Guid.NewGuid();
    private static readonly Guid OtherCustomerEntityId = Guid.NewGuid();

    private OrderApplicationService BuildSut() => new(_orders, _products, _uow, _currentUser);

    private void AsAdmin()
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(AdminUserId);
        _currentUser.CustomerId.Returns((Guid?)null);
        _currentUser.Role.Returns(Roles.Admin);
        _currentUser.IsAdmin.Returns(true);
        _currentUser.IsCustomer.Returns(false);
    }

    private void AsCustomer(Guid? customerEntityId = null)
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(CustomerUserId);
        _currentUser.CustomerId.Returns(customerEntityId ?? CustomerEntityId);
        _currentUser.Role.Returns(Roles.Customer);
        _currentUser.IsAdmin.Returns(false);
        _currentUser.IsCustomer.Returns(true);
    }

    private static Product MakeProduct(Guid id, decimal price = 10m, int stock = 5, string currency = "USD")
        => new(id, $"Product-{id}", price, currency, stock);

    private static Order MakeOrder(Guid customerId, IReadOnlyList<(Guid productId, decimal price, int qty)> items, string currency = "USD")
    {
        var newItems = items.Select(i => new NewOrderItem(i.productId, i.price, i.qty)).ToList();
        return Order.Place(customerId, currency, newItems).Value!;
    }

    [Fact]
    public async Task CreateAsync_HappyPath_PersistsAndReturnsResponse()
    {
        AsAdmin();
        var productId = Guid.NewGuid();
        var product = MakeProduct(productId, price: 12.50m, stock: 10);
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { product });

        var request = new CreateOrderRequest(
            CustomerEntityId, "USD",
            new[] { new CreateOrderItemRequest(productId, 3) });

        var result = await BuildSut().CreateAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CustomerId.Should().Be(CustomerEntityId);
        result.Value.Currency.Should().Be("USD");
        result.Value.Total.Should().Be(12.50m * 3);
        result.Value.Status.Should().Be(nameof(OrderStatus.Placed));
        result.Value.Items.Should().ContainSingle().Which.UnitPrice.Should().Be(12.50m);

        _orders.Received(1).Add(Arg.Any<Order>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_EmptyItems_ReturnsValidationError()
    {
        AsAdmin();
        var request = new CreateOrderRequest(CustomerEntityId, "USD", Array.Empty<CreateOrderItemRequest>());

        var result = await BuildSut().CreateAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("validation_error");
        _orders.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Theory]
    [InlineData("us")]
    [InlineData("usd")]
    [InlineData("USDX")]
    [InlineData("")]
    public async Task CreateAsync_InvalidCurrency_ReturnsValidationError(string currency)
    {
        AsAdmin();
        var request = new CreateOrderRequest(
            CustomerEntityId, currency,
            new[] { new CreateOrderItemRequest(Guid.NewGuid(), 1) });

        var result = await BuildSut().CreateAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("validation_error");
    }

    [Fact]
    public async Task CreateAsync_ProductNotFound_ReturnsNotFound()
    {
        AsAdmin();
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Product>());

        var request = new CreateOrderRequest(
            CustomerEntityId, "USD",
            new[] { new CreateOrderItemRequest(Guid.NewGuid(), 1) });

        var result = await BuildSut().CreateAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("not_found");
        _orders.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Fact]
    public async Task CreateAsync_InsufficientStock_ReturnsConflict()
    {
        AsAdmin();
        var productId = Guid.NewGuid();
        var product = MakeProduct(productId, stock: 2);
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { product });

        var request = new CreateOrderRequest(
            CustomerEntityId, "USD",
            new[] { new CreateOrderItemRequest(productId, 5) });

        var result = await BuildSut().CreateAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("conflict");
        _orders.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Fact]
    public async Task CreateAsync_CustomerForAnotherCustomer_IsForbidden()
    {
        AsCustomer();
        var request = new CreateOrderRequest(
            OtherCustomerEntityId, "USD",
            new[] { new CreateOrderItemRequest(Guid.NewGuid(), 1) });

        var result = await BuildSut().CreateAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("forbidden");
        _orders.DidNotReceiveWithAnyArgs().Add(default!);
    }

    [Fact]
    public async Task CreateAsync_CustomerOmitsCustomerId_UsesOwnId()
    {
        AsCustomer();
        var productId = Guid.NewGuid();
        var product = MakeProduct(productId);
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { product });

        var request = new CreateOrderRequest(
            CustomerId: null, "USD",
            new[] { new CreateOrderItemRequest(productId, 1) });

        var result = await BuildSut().CreateAsync(request, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.CustomerId.Should().Be(CustomerEntityId);
    }

    [Fact]
    public async Task ConfirmAsync_HappyPath_DecrementsStockAndTransitions()
    {
        AsAdmin();
        var productId = Guid.NewGuid();
        var order = MakeOrder(CustomerEntityId, new[] { (productId, 10m, 3) });
        var product = MakeProduct(productId, price: 10m, stock: 10);

        _orders.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { product });

        var result = await BuildSut().ConfirmAsync(order.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(nameof(OrderStatus.Confirmed));
        product.AvailableQuantity.Should().Be(7);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmAsync_AlreadyConfirmed_IsIdempotentWithoutStockChanges()
    {
        AsAdmin();
        var productId = Guid.NewGuid();
        var order = MakeOrder(CustomerEntityId, new[] { (productId, 10m, 3) });
        order.Confirm();

        _orders.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await BuildSut().ConfirmAsync(order.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(nameof(OrderStatus.Confirmed));
        await _products.DidNotReceiveWithAnyArgs().GetByIdsAsync(default!, default);
        await _uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task ConfirmAsync_OrderNotFound_ReturnsNotFound()
    {
        AsAdmin();
        _orders.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await BuildSut().ConfirmAsync(Guid.NewGuid(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("not_found");
    }

    [Fact]
    public async Task ConfirmAsync_ConcurrencyConflict_ReturnsConflict()
    {
        AsAdmin();
        var productId = Guid.NewGuid();
        var order = MakeOrder(CustomerEntityId, new[] { (productId, 10m, 1) });
        var product = MakeProduct(productId, stock: 5);

        _orders.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { product });
        _uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ => throw new ConcurrencyConflictException("boom"));

        var result = await BuildSut().ConfirmAsync(order.Id, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("conflict");
    }

    [Fact]
    public async Task CancelAsync_FromConfirmed_RestoresStock()
    {
        AsAdmin();
        var productId = Guid.NewGuid();
        var order = MakeOrder(CustomerEntityId, new[] { (productId, 10m, 4) });
        order.Confirm();
        var product = MakeProduct(productId, stock: 6);

        _orders.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);
        _products.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { product });

        var result = await BuildSut().CancelAsync(order.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(nameof(OrderStatus.Canceled));
        product.AvailableQuantity.Should().Be(10);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelAsync_FromPlaced_DoesNotTouchStock()
    {
        AsAdmin();
        var productId = Guid.NewGuid();
        var order = MakeOrder(CustomerEntityId, new[] { (productId, 10m, 4) });

        _orders.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await BuildSut().CancelAsync(order.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be(nameof(OrderStatus.Canceled));
        await _products.DidNotReceiveWithAnyArgs().GetByIdsAsync(default!, default);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CancelAsync_AlreadyCanceled_IsIdempotentWithoutSave()
    {
        AsAdmin();
        var productId = Guid.NewGuid();
        var order = MakeOrder(CustomerEntityId, new[] { (productId, 10m, 1) });
        order.Cancel();

        _orders.GetByIdAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await BuildSut().CancelAsync(order.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _products.DidNotReceiveWithAnyArgs().GetByIdsAsync(default!, default);
        await _uow.DidNotReceiveWithAnyArgs().SaveChangesAsync(default);
    }

    [Fact]
    public async Task GetByIdAsync_CustomerCannotSeeOtherCustomersOrder_ReturnsNotFound()
    {
        AsCustomer();
        var order = MakeOrder(OtherCustomerEntityId, new[] { (Guid.NewGuid(), 10m, 1) });
        _orders.GetByIdReadOnlyAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await BuildSut().GetByIdAsync(order.Id, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("not_found");
    }

    [Fact]
    public async Task GetByIdAsync_CustomerSeesOwnOrder_ReturnsOk()
    {
        AsCustomer();
        var order = MakeOrder(CustomerEntityId, new[] { (Guid.NewGuid(), 10m, 1) });
        _orders.GetByIdReadOnlyAsync(order.Id, Arg.Any<CancellationToken>()).Returns(order);

        var result = await BuildSut().GetByIdAsync(order.Id, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Id.Should().Be(order.Id);
    }

    [Fact]
    public async Task ListAsync_ClampsPageSizeTo100()
    {
        AsAdmin();
        _orders.ListAsync(
            Arg.Any<Guid?>(), Arg.Any<OrderStatus?>(),
            Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(),
            Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<CancellationToken>())
            .Returns((Array.Empty<Order>(), 0));

        var query = new ListOrdersQuery(null, null, null, null, Page: 1, PageSize: 500);

        var result = await BuildSut().ListAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.PageSize.Should().Be(100);
        await _orders.Received(1).ListAsync(
            Arg.Any<Guid?>(), Arg.Any<OrderStatus?>(),
            Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(),
            1, 100, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAsync_CustomerScopesToOwnId()
    {
        AsCustomer();
        _orders.ListAsync(
            Arg.Any<Guid?>(), Arg.Any<OrderStatus?>(),
            Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(),
            Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<CancellationToken>())
            .Returns((Array.Empty<Order>(), 0));

        var query = new ListOrdersQuery(null, null, null, null, 1, 20);

        var result = await BuildSut().ListAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _orders.Received(1).ListAsync(
            CustomerEntityId, Arg.Any<OrderStatus?>(),
            Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAsync_CustomerAskingForAnotherCustomersList_IsForbidden()
    {
        AsCustomer();
        var query = new ListOrdersQuery(OtherCustomerEntityId, null, null, null, 1, 20);

        var result = await BuildSut().ListAsync(query, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("forbidden");
    }
}
