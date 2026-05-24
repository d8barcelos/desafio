using OrderService.Domain.Common;

namespace OrderService.Domain.Errors;

public static class DomainErrors
{
    public static class Order
    {
        public static readonly Error NoItems = new("order.no_items", "Order must have at least one item.");
        public static readonly Error InvalidQuantity = new("order.invalid_quantity", "Item quantity must be greater than zero.");
        public static readonly Error InvalidTransition = new("order.invalid_transition", "Invalid order status transition.");
    }

    public static class Product
    {
        public static readonly Error InvalidQuantity = new("product.invalid_quantity", "Quantity must be greater than zero.");
        public static readonly Error InsufficientStock = new("product.insufficient_stock", "Insufficient stock available.");
    }
}
