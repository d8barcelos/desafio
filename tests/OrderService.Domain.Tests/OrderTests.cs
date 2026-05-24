using FluentAssertions;
using OrderService.Domain.Entities;
using OrderService.Domain.Errors;

namespace OrderService.Domain.Tests;

public class OrderTests
{
    private static readonly Guid CustomerId = Guid.NewGuid();
    private static readonly Guid ProductId = Guid.NewGuid();
    private const string Currency = "USD";

    private static NewOrderItem ValidItem(int quantity = 2, decimal unitPrice = 10m)
        => new(ProductId, unitPrice, quantity);

    [Fact]
    public void Place_WithoutItems_Fails()
    {
        var result = Order.Place(CustomerId, Currency, Array.Empty<NewOrderItem>());

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(DomainErrors.Order.NoItems);
    }

    [Fact]
    public void Place_WithNullItems_Fails()
    {
        var result = Order.Place(CustomerId, Currency, null!);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(DomainErrors.Order.NoItems);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-99)]
    public void Place_WithNonPositiveQuantity_Fails(int quantity)
    {
        var items = new[] { ValidItem(quantity: quantity) };

        var result = Order.Place(CustomerId, Currency, items);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(DomainErrors.Order.InvalidQuantity);
    }

    [Fact]
    public void Place_ValidInput_SetsStateToPlacedAndComputesTotal()
    {
        var items = new[]
        {
            new NewOrderItem(ProductId, 10.00m, 2),
            new NewOrderItem(Guid.NewGuid(), 5.50m, 3),
        };

        var result = Order.Place(CustomerId, Currency, items);

        result.IsSuccess.Should().BeTrue();
        var order = result.Value!;
        order.Status.Should().Be(OrderStatus.Placed);
        order.CustomerId.Should().Be(CustomerId);
        order.Currency.Should().Be(Currency);
        order.Total.Should().Be(10.00m * 2 + 5.50m * 3);
        order.Items.Should().HaveCount(2);
        order.Items.Should().AllSatisfy(i => i.OrderId.Should().Be(order.Id));
    }

    [Fact]
    public void Place_AssignsNewIds_ForOrderAndItems()
    {
        var items = new[] { ValidItem() };

        var order = Order.Place(CustomerId, Currency, items).Value!;

        order.Id.Should().NotBeEmpty();
        order.Items.Should().AllSatisfy(i => i.Id.Should().NotBeEmpty());
    }

    [Fact]
    public void Confirm_FromPlaced_SucceedsAndTransitionsToConfirmed()
    {
        var order = Order.Place(CustomerId, Currency, new[] { ValidItem() }).Value!;

        var result = order.Confirm();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public void Confirm_FromConfirmed_IsIdempotent()
    {
        var order = Order.Place(CustomerId, Currency, new[] { ValidItem() }).Value!;
        order.Confirm();

        var result = order.Confirm();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public void Confirm_FromCanceled_Fails()
    {
        var order = Order.Place(CustomerId, Currency, new[] { ValidItem() }).Value!;
        order.Cancel();

        var result = order.Confirm();

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(DomainErrors.Order.InvalidTransition);
        order.Status.Should().Be(OrderStatus.Canceled);
    }

    [Fact]
    public void Cancel_FromPlaced_Succeeds()
    {
        var order = Order.Place(CustomerId, Currency, new[] { ValidItem() }).Value!;

        var result = order.Cancel();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Canceled);
    }

    [Fact]
    public void Cancel_FromConfirmed_Succeeds()
    {
        var order = Order.Place(CustomerId, Currency, new[] { ValidItem() }).Value!;
        order.Confirm();

        var result = order.Cancel();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Canceled);
    }

    [Fact]
    public void Cancel_FromCanceled_IsIdempotent()
    {
        var order = Order.Place(CustomerId, Currency, new[] { ValidItem() }).Value!;
        order.Cancel();

        var result = order.Cancel();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Canceled);
    }
}
