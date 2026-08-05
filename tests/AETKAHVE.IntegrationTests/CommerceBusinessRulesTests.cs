using System.Text;
using System.Text.Json;
using AETKAHVE.Application.Commerce;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Domain.Common;
using AETKAHVE.Infrastructure.Commerce;
using AETKAHVE.Infrastructure.Identity;
using AETKAHVE.Infrastructure.Persistence;
using AETKAHVE.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AETKAHVE.IntegrationTests;

public sealed class CommerceBusinessRulesTests(AeternumWebApplicationFactory factory) : IClassFixture<AeternumWebApplicationFactory>
{
    [Fact]
    public async Task Catalog_filters_projects_pages_and_scopes_favorites()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var customer = await GetUserAsync(services, AeternumWebApplicationFactory.CustomerEmail);
        var other = await GetUserAsync(services, AeternumWebApplicationFactory.ResetEmail);
        var token = Guid.NewGuid().ToString("N");
        var first = await CreateProductAsync(services, $"Filter {token} Alpha", 75, 3);
        await CreateProductAsync(services, $"Filter {token} Beta", 125, 0);
        await CreateProductAsync(services, $"Unrelated {Guid.NewGuid():N}", 10, 2);

        var catalog = services.GetRequiredService<ICatalogQueryService>();
        var page = await catalog.SearchAsync(new ProductQuery(Search: token, MaximumPrice: 100, InStockOnly: true, Sort: "price-asc", PageSize: 1), customer.Id, default);

        Assert.Equal(1, page.TotalCount);
        Assert.Equal(first.ProductId, Assert.Single(page.Items).Id);
        Assert.StartsWith("/", page.Items[0].ImageUrl, StringComparison.Ordinal);

        var favorites = services.GetRequiredService<IFavoriteService>();
        Assert.True(await favorites.ToggleAsync(customer.Id, first.ProductId, default));
        Assert.True(Assert.Single((await favorites.GetAsync(customer.Id, 1, 10, default)).Items).IsFavorite);
        Assert.Empty((await favorites.GetAsync(other.Id, 1, 10, default)).Items);
        Assert.False(await favorites.ToggleAsync(customer.Id, first.ProductId, default));
    }

    [Fact]
    public async Task Pricing_chooses_best_campaign_combines_allowed_coupon_and_never_goes_negative()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var user = await GetUserAsync(services, AeternumWebApplicationFactory.CustomerEmail);
        var product = await CreateProductAsync(services, $"Pricing {Guid.NewGuid():N}", 100, 10, 10);
        var db = services.GetRequiredService<AppDbContext>();
        var now = factory.Clock.GetUtcNow();
        db.Campaigns.AddRange(
            Campaign("Ten percent", DiscountType.Percentage, 10, now, true),
            Campaign("Best fixed", DiscountType.FixedAmount, 20, now, true),
            Campaign("Expired", DiscountType.FixedAmount, 99, now.AddYears(-2), true, now.AddYears(-1)));
        var code = $"C{Guid.NewGuid():N}".ToUpperInvariant();
        db.Coupons.Add(new Coupon
        {
            Name = "Combinable",
            Code = code,
            DiscountType = DiscountType.FixedAmount,
            DiscountValue = 5,
            StartDateUtc = now.AddDays(-1),
            EndDateUtc = now.AddDays(1),
            IsActive = true,
            CanCombineWithOtherDiscounts = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
        await db.SaveChangesAsync();

        var carts = services.GetRequiredService<ICartService>();
        await carts.ClearAsync(new CartOwner(user.Id, null), default);
        await carts.AddAsync(new CartOwner(user.Id, null), product.ProductId, product.VariantId, 1, default);
        var priced = await carts.ApplyCouponAsync(new CartOwner(user.Id, null), code, default);

        Assert.Equal(100m, priced.Subtotal);
        Assert.Equal(25m, priced.DiscountTotal);
        Assert.Equal(7.50m, priced.TaxTotal);
        Assert.Equal(82.50m, priced.GrandTotal);
    }

    [Fact]
    public async Task Coupon_combination_and_usage_limits_are_enforced_without_persisting_invalid_code()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var user = await GetUserAsync(services, AeternumWebApplicationFactory.CustomerEmail);
        var product = await CreateProductAsync(services, $"Coupon {Guid.NewGuid():N}", 100, 5);
        var db = services.GetRequiredService<AppDbContext>();
        var now = factory.Clock.GetUtcNow();
        var campaign = Campaign("Exclusive", DiscountType.FixedAmount, 90, now, false);
        var incompatibleCode = $"I{Guid.NewGuid():N}".ToUpperInvariant();
        var limitedCode = $"L{Guid.NewGuid():N}".ToUpperInvariant();
        var incompatible = new Coupon
        {
            Name = "Incompatible",
            Code = incompatibleCode,
            DiscountType = DiscountType.FixedAmount,
            DiscountValue = 5,
            StartDateUtc = now.AddDays(-1),
            EndDateUtc = now.AddDays(1),
            IsActive = true,
            CanCombineWithOtherDiscounts = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        var limited = new Coupon
        {
            Name = "Limited",
            Code = limitedCode,
            DiscountType = DiscountType.FixedAmount,
            DiscountValue = 5,
            StartDateUtc = now.AddDays(-1),
            EndDateUtc = now.AddDays(1),
            IsActive = true,
            CanCombineWithOtherDiscounts = true,
            TotalUsageLimit = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        var historicalOrder = new Order
        {
            OrderNumber = $"COUPON-{Guid.NewGuid():N}",
            UserId = user.Id,
            BillingAddressSnapshot = "{}",
            ShippingAddressSnapshot = "{}",
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        db.Campaigns.Add(campaign);
        db.Coupons.AddRange(incompatible, limited);
        db.Orders.Add(historicalOrder);
        db.CouponUsages.Add(new CouponUsage { Coupon = limited, Order = historicalOrder, UserId = user.Id, Status = CouponUsageStatus.Consumed, CreatedAtUtc = now, UpdatedAtUtc = now });
        await db.SaveChangesAsync();
        var carts = services.GetRequiredService<ICartService>();
        await carts.ClearAsync(new CartOwner(user.Id, null), default);
        await carts.AddAsync(new CartOwner(user.Id, null), product.ProductId, product.VariantId, 1, default);

        await Assert.ThrowsAsync<CommerceRuleException>(() => carts.ApplyCouponAsync(new CartOwner(user.Id, null), incompatibleCode, default));
        Assert.Null((await carts.GetAsync(new CartOwner(user.Id, null), default)).CouponCode);
        campaign.IsActive = false;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<CommerceRuleException>(() => carts.ApplyCouponAsync(new CartOwner(user.Id, null), limitedCode, default));
        Assert.Null((await carts.GetAsync(new CartOwner(user.Id, null), default)).CouponCode);
    }

    [Fact]
    public async Task Inventory_concurrency_token_prevents_lost_update_and_negative_stock()
    {
        await using var seedScope = factory.Services.CreateAsyncScope();
        var product = await CreateProductAsync(seedScope.ServiceProvider, $"Concurrency {Guid.NewGuid():N}", 50, 1);

        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var firstDb = firstScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var secondDb = secondScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var first = await firstDb.ProductVariants.SingleAsync(x => x.Id == product.VariantId);
        var second = await secondDb.ProductVariants.SingleAsync(x => x.Id == product.VariantId);
        first.AdjustStock(-1);
        second.AdjustStock(-1);
        await firstDb.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondDb.SaveChangesAsync());

        await using var verifyScope = factory.Services.CreateAsyncScope();
        Assert.Equal(0, (await verifyScope.ServiceProvider.GetRequiredService<AppDbContext>().ProductVariants.AsNoTracking()
            .SingleAsync(x => x.Id == product.VariantId)).StockQuantity);
    }

    [Fact]
    public async Task Return_requires_owned_delivered_order_and_restocks_only_after_received_approval()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var user = await GetUserAsync(services, AeternumWebApplicationFactory.CustomerEmail);
        var other = await GetUserAsync(services, AeternumWebApplicationFactory.ResetEmail);
        var purchase = await CreateDeliveredPurchaseAsync(services, user.Id, 2, 0);
        var returns = services.GetRequiredService<IReturnService>();
        var input = new ReturnCreateRequest(user.Id, purchase.OrderId, "Defective", null,
            [new ReturnItemInput(purchase.OrderItemId, 1, "Package defect", ReturnItemCondition.Defective, null)]);

        await Assert.ThrowsAsync<CommerceRuleException>(() => returns.CreateAsync(input with { UserId = other.Id }, default));
        var returnId = await returns.CreateAsync(input, default);
        var admin = await GetUserAsync(services, AeternumWebApplicationFactory.AdminEmail);
        Assert.True((await returns.DecideAsync(new ReturnDecision(returnId, admin.Id, ReturnStatus.Approved, "Approved", false), default)).Succeeded);
        Assert.Equal(0, (await services.GetRequiredService<AppDbContext>().ProductVariants.SingleAsync(x => x.Id == purchase.VariantId)).StockQuantity);
        Assert.True((await returns.DecideAsync(new ReturnDecision(returnId, admin.Id, ReturnStatus.ProductReceived, "Received", true), default)).Succeeded);
        Assert.Equal(1, (await services.GetRequiredService<AppDbContext>().ProductVariants.SingleAsync(x => x.Id == purchase.VariantId)).StockQuantity);
        Assert.False((await returns.DecideAsync(new ReturnDecision(returnId, admin.Id, ReturnStatus.ProductReceived, "Duplicate", true), default)).Succeeded);
        Assert.Single(await services.GetRequiredService<AppDbContext>().StockMovements.Where(x => x.ReferenceId == returnId && x.MovementType == StockMovementType.Return).ToListAsync());
    }

    [Fact]
    public async Task Review_requires_owned_delivered_purchase_and_allows_one_review_per_item()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var user = await GetUserAsync(services, AeternumWebApplicationFactory.CustomerEmail);
        var other = await GetUserAsync(services, AeternumWebApplicationFactory.ResetEmail);
        var purchase = await CreateDeliveredPurchaseAsync(services, user.Id, 1, 0);
        var reviews = services.GetRequiredService<IReviewService>();

        await Assert.ThrowsAsync<CommerceRuleException>(() => reviews.CreateOrUpdateAsync(new ReviewInput(other.Id, purchase.OrderItemId, 5, "No ownership"), default));
        var reviewId = await reviews.CreateOrUpdateAsync(new ReviewInput(user.Id, purchase.OrderItemId, 5, "Excellent coffee"), default);
        await Assert.ThrowsAsync<CommerceRuleException>(() => reviews.CreateOrUpdateAsync(new ReviewInput(user.Id, purchase.OrderItemId, 4, "Second review"), default));
        Assert.NotEqual(Guid.Empty, reviewId);
    }

    [Fact]
    public async Task Customer_order_details_expose_owned_order_item_ids_for_return_and_review_actions()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var user = await GetUserAsync(services, AeternumWebApplicationFactory.CustomerEmail);
        var purchase = await CreateDeliveredPurchaseAsync(services, user.Id, 2, 0);

        var details = await services.GetRequiredService<IOrderService>().GetForUserAsync(user.Id, purchase.OrderId, default);

        Assert.NotNull(details);
        var line = Assert.Single(details.Items);
        Assert.Equal(purchase.OrderItemId, line.OrderItemId);
        Assert.Equal(2, line.Quantity);
    }

    [Fact]
    public async Task Reporting_separates_components_refunds_and_emits_utf8_safe_csv()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var user = await GetUserAsync(services, AeternumWebApplicationFactory.CustomerEmail);
        var db = services.GetRequiredService<AppDbContext>();
        var paidAt = new DateTimeOffset(2030, 1, 15, 10, 0, 0, TimeSpan.Zero);
        var order = new Order
        {
            OrderNumber = $"REPORT-{Guid.NewGuid():N}",
            UserId = user.Id,
            Status = OrderStatus.PaymentReceived,
            PaymentStatus = PaymentStatus.Succeeded,
            BillingAddressSnapshot = "{}",
            ShippingAddressSnapshot = "{}",
            Subtotal = 100,
            DiscountTotal = 10,
            TaxTotal = 18,
            ShippingTotal = 5,
            GrandTotal = 113,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            PaidAtUtc = paidAt,
            CreatedAtUtc = paidAt,
            UpdatedAtUtc = paidAt,
        };
        var payment = new Payment
        {
            Order = order,
            Provider = "Mock",
            TransactionId = $"report-{Guid.NewGuid():N}",
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            Amount = 113,
            Status = PaymentStatus.Succeeded,
            CompletedAtUtc = paidAt,
            CreatedAtUtc = paidAt,
            UpdatedAtUtc = paidAt,
        };
        db.Orders.Add(order);
        db.Payments.Add(payment);
        db.Refunds.Add(new Refund { Payment = payment, Amount = 13, Status = RefundStatus.Succeeded, CompletedAtUtc = paidAt.AddHours(1), CreatedAtUtc = paidAt, UpdatedAtUtc = paidAt });
        await db.SaveChangesAsync();

        var reporting = services.GetRequiredService<IReportingService>();
        var filter = new ReportFilter(paidAt.AddDays(-1), paidAt.AddDays(1));
        var report = await reporting.GetSalesAsync(filter, default);
        Assert.Equal(new SalesReport(100, 10, 18, 5, 13, 100, 1, 113), report);
        var csv = await reporting.ExportSalesCsvAsync(filter, default);
        Assert.Equal(Encoding.UTF8.GetPreamble(), csv[..3]);
        Assert.Contains("\"NetRevenue\",100", Encoding.UTF8.GetString(csv), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Notification_outbox_is_delivered_once_by_mock_processor()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var user = await GetUserAsync(services, AeternumWebApplicationFactory.CustomerEmail);
        var db = services.GetRequiredService<AppDbContext>();
        var now = factory.Clock.GetUtcNow();
        var destination = $"outbox-{Guid.NewGuid():N}@test.local";
        var delivery = new NotificationDelivery
        {
            UserId = user.Id,
            Channel = NotificationChannel.Email,
            Destination = destination,
            TemplateKey = "Test",
            PayloadJson = JsonSerializer.Serialize(new DeliveryPayload("Subject", "Body")),
            NextAttemptAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        db.NotificationDeliveries.Add(delivery);
        await db.SaveChangesAsync();

        await services.GetRequiredService<NotificationDeliveryProcessor>().ProcessBatchAsync(default);
        await db.Entry(delivery).ReloadAsync();
        Assert.Equal(DeliveryStatus.Delivered, delivery.Status);
        Assert.Equal(1, delivery.AttemptCount);
        Assert.Contains(services.GetRequiredService<MockEmailSender>().Sent, x => x.Destination == destination);
        await services.GetRequiredService<NotificationDeliveryProcessor>().ProcessBatchAsync(default);
        Assert.Equal(1, delivery.AttemptCount);
    }

    private static Campaign Campaign(string name, DiscountType type, decimal value, DateTimeOffset start, bool combinable, DateTimeOffset? end = null) => new()
    {
        Name = $"{name} {Guid.NewGuid():N}",
        Slug = $"campaign-{Guid.NewGuid():N}",
        DiscountType = type,
        DiscountValue = value,
        StartDateUtc = start.AddDays(-1),
        EndDateUtc = end ?? start.AddDays(1),
        IsActive = true,
        CanCombineWithOtherDiscounts = combinable,
        CreatedAtUtc = start,
        UpdatedAtUtc = start,
    };

    private static async Task<ApplicationUser> GetUserAsync(IServiceProvider services, string email) =>
        await services.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"Test user {email} was not found.");

    private static async Task<(Guid ProductId, Guid VariantId)> CreateProductAsync(IServiceProvider services, string name, decimal price, int stock, decimal taxRate = 0)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;
        var token = Guid.NewGuid().ToString("N");
        var category = new Category { Name = $"Category {token}", Slug = $"category-{token}", CreatedAtUtc = now, UpdatedAtUtc = now };
        var product = new Product
        {
            Name = name,
            Slug = $"product-{token}",
            Sku = $"SKU-{token.ToUpperInvariant()}",
            ShortDescription = "Test",
            Description = name,
            BasePrice = price,
            TaxRate = taxRate,
            Category = category,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        var variant = new ProductVariant
        {
            Product = product,
            Weight = 250,
            Unit = WeightUnit.Gram,
            Sku = $"VAR-{token.ToUpperInvariant()}",
            Price = price,
            StockQuantity = stock,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        product.Variants.Add(variant);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return (product.Id, variant.Id);
    }

    private static async Task<(Guid OrderId, Guid OrderItemId, Guid VariantId)> CreateDeliveredPurchaseAsync(IServiceProvider services, Guid userId, int quantity, int remainingStock)
    {
        var product = await CreateProductAsync(services, $"Delivered {Guid.NewGuid():N}", 100, remainingStock);
        var db = services.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;
        var order = new Order
        {
            OrderNumber = $"DELIVERED-{Guid.NewGuid():N}",
            UserId = userId,
            Status = OrderStatus.Delivered,
            PaymentStatus = PaymentStatus.Succeeded,
            ShippingStatus = ShipmentStatus.Delivered,
            BillingAddressSnapshot = "{}",
            ShippingAddressSnapshot = "{}",
            Subtotal = 100 * quantity,
            GrandTotal = 100 * quantity,
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            PaidAtUtc = now.AddDays(-3),
            DeliveredAtUtc = now.AddDays(-1),
            CreatedAtUtc = now.AddDays(-3),
            UpdatedAtUtc = now,
        };
        var item = new OrderItem
        {
            Order = order,
            ProductId = product.ProductId,
            ProductVariantId = product.VariantId,
            ProductName = "Delivered product",
            Sku = $"ITEM-{Guid.NewGuid():N}".ToUpperInvariant(),
            UnitPrice = 100,
            Quantity = quantity,
            LineTotal = 100 * quantity,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        order.Items.Add(item);
        var payment = new Payment
        {
            Order = order,
            Provider = "Mock",
            TransactionId = $"delivered-{Guid.NewGuid():N}",
            IdempotencyKey = Guid.NewGuid().ToString("N"),
            Amount = order.GrandTotal,
            Status = PaymentStatus.Succeeded,
            CompletedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        db.Orders.Add(order);
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        return (order.Id, item.Id, product.VariantId);
    }
}
