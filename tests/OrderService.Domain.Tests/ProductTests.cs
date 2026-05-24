using FluentAssertions;
using OrderService.Domain.Entities;
using OrderService.Domain.Errors;

namespace OrderService.Domain.Tests;

public class ProductTests
{
    private static Product BuildProduct(int availableQuantity = 10)
        => new(Guid.NewGuid(), "Widget", 9.99m, "USD", availableQuantity);

    [Fact]
    public void DecrementStock_WhenSufficient_ReducesAvailableQuantity()
    {
        var product = BuildProduct(availableQuantity: 5);

        var result = product.DecrementStock(3);

        result.IsSuccess.Should().BeTrue();
        product.AvailableQuantity.Should().Be(2);
    }

    [Fact]
    public void DecrementStock_WhenInsufficient_Fails()
    {
        var product = BuildProduct(availableQuantity: 2);

        var result = product.DecrementStock(5);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(DomainErrors.Product.InsufficientStock);
        product.AvailableQuantity.Should().Be(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void DecrementStock_NonPositiveQuantity_Fails(int quantity)
    {
        var product = BuildProduct(availableQuantity: 10);

        var result = product.DecrementStock(quantity);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(DomainErrors.Product.InvalidQuantity);
        product.AvailableQuantity.Should().Be(10);
    }

    [Fact]
    public void RestoreStock_AddsBackQuantity()
    {
        var product = BuildProduct(availableQuantity: 3);

        product.RestoreStock(7);

        product.AvailableQuantity.Should().Be(10);
    }

    [Fact]
    public void RestoreStock_IgnoresNonPositiveQuantity()
    {
        var product = BuildProduct(availableQuantity: 3);

        product.RestoreStock(0);
        product.RestoreStock(-1);

        product.AvailableQuantity.Should().Be(3);
    }
}
