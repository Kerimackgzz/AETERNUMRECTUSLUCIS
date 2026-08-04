using AETKAHVE.Domain.Common;
using AETKAHVE.Domain.Commerce;

namespace AETKAHVE.UnitTests;

public sealed class CommerceDomainTests
{
    [Fact]
    public void Product_rejects_invalid_price_and_stock()
    {
        var product = ValidProduct();
        product.DiscountedPrice = 120;
        Assert.Throws<CommerceRuleException>(product.Validate);
        product.DiscountedPrice = 80;
        product.Validate();
        Assert.Throws<CommerceRuleException>(() => product.AdjustStock(-2));
    }

    [Fact]
    public void Stock_adjustment_rotates_concurrency_token()
    {
        var product = ValidProduct();
        product.StockQuantity = 4;
        var original = product.ConcurrencyToken;
        product.AdjustStock(-2);
        Assert.Equal(2, product.StockQuantity);
        Assert.NotEqual(original, product.ConcurrencyToken);
    }

    [Fact]
    public void Order_state_machine_allows_only_declared_transitions()
    {
        var now = DateTimeOffset.UtcNow;
        var order = new Order { Id = Guid.NewGuid(), Status = OrderStatus.PendingPayment, CreatedAtUtc = now, UpdatedAtUtc = now };
        order.TransitionTo(OrderStatus.PaymentReceived, Guid.NewGuid(), now, "paid");
        Assert.Equal(OrderStatus.PaymentReceived, order.Status);
        Assert.Single(order.StatusHistory);
        Assert.Throws<CommerceRuleException>(() => order.TransitionTo(OrderStatus.Delivered, null, now, "invalid"));
    }

    [Theory]
    [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled, false)]
    [InlineData(OrderStatus.Packed, OrderStatus.Cancelled, true)]
    [InlineData(OrderStatus.Delivered, OrderStatus.ReturnRequested, true)]
    public void Order_transition_matrix_is_explicit(OrderStatus current, OrderStatus next, bool expected) =>
        Assert.Equal(expected, OrderStatusRules.CanTransition(current, next));

    [Theory]
    [InlineData("Bad Slug", "SKU-1")]
    [InlineData("valid-slug", "sku-1")]
    [InlineData("double--hyphen", "SKU-1")]
    public void Product_rejects_noncanonical_slug_or_sku(string slug, string sku)
    {
        var product = ValidProduct();
        product.Slug = slug;
        product.Sku = sku;
        Assert.Throws<CommerceRuleException>(product.Validate);
    }

    [Fact]
    public void Variant_rejects_invalid_weight_price_sku_and_stock()
    {
        var variant = new ProductVariant { Weight = 0, Sku = "bad sku", Price = 10, DiscountedPrice = 20, StockQuantity = -1 };
        Assert.Throws<CommerceRuleException>(variant.Validate);
        variant.Weight = 250;
        variant.Sku = "ARL-250";
        variant.Price = 20;
        variant.DiscountedPrice = 10;
        variant.StockQuantity = 1;
        variant.Validate();
    }

    private static Product ValidProduct() => new()
    {
        Name = "Valid Product",
        Slug = "valid-product",
        Sku = "SKU-1",
        BasePrice = 100,
        StockQuantity = 1,
    };
}
