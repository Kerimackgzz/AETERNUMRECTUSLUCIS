using AETKAHVE.Application.Commerce;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Infrastructure.Identity;
using AETKAHVE.Infrastructure.Persistence;
using AETKAHVE.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AETKAHVE.IntegrationTests;

public sealed class CommerceFlowTests(AeternumWebApplicationFactory factory) : IClassFixture<AeternumWebApplicationFactory>
{
    [Fact]
    public async Task Cart_merge_caps_quantity_and_preserves_single_user_cart()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var user = await GetCustomerAsync(scope.ServiceProvider);
        var product = await CreateProductAsync(scope.ServiceProvider, 5);
        var carts = scope.ServiceProvider.GetRequiredService<ICartService>();
        await carts.ClearAsync(new CartOwner(user.Id, null), default);
        var guest = Guid.NewGuid();
        await carts.AddAsync(new CartOwner(null, guest), product.ProductId, product.VariantId, 4, default);
        await carts.AddAsync(new CartOwner(user.Id, null), product.ProductId, product.VariantId, 3, default);

        var merged = await carts.MergeGuestCartAsync(user.Id, guest, default);

        Assert.Equal(5, Assert.Single(merged.Cart.Items).Quantity);
        Assert.Single(merged.Warnings);
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Carts.AnyAsync(x => x.GuestToken == guest));
    }

    [Fact]
    public async Task Successful_payment_is_idempotent_and_decrements_stock_once()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var user = await GetCustomerAsync(services);
        var product = await CreateProductAsync(services, 5);
        await services.GetRequiredService<ICartService>().ClearAsync(new CartOwner(user.Id, null), default);
        var address = await services.GetRequiredService<IAddressService>().SaveAsync(user.Id, null,
            new AddressInput("Ev", "Test", "Customer", "+905551112233", "Türkiye", "İstanbul", "Kadıköy", null, "34000", "Test adresi", true, true), default);
        var cart = await services.GetRequiredService<ICartService>().AddAsync(new CartOwner(user.Id, null), product.ProductId, product.VariantId, 2, default);
        var checkout = services.GetRequiredService<ICheckoutService>();
        var initialized = await checkout.InitializeAsync(new CheckoutRequest(user.Id, cart.CartId, address.Id, address.Id, Guid.NewGuid().ToString("N"), null), "https://localhost/payments/Mock/callback", default);

        var completed = await checkout.CompleteAsync("Mock", new PaymentCallbackRequest(initialized.RequestReference, $"tx-{Guid.NewGuid():N}", "success"), default);
        var replay = await checkout.CompleteAsync("Mock", new PaymentCallbackRequest(initialized.RequestReference, "different-tx", "success"), default);

        Assert.Equal(OrderStatus.PaymentReceived, completed.OrderStatus);
        Assert.False(completed.IsIdempotentReplay);
        Assert.True(replay.IsIdempotentReplay);
        var db = services.GetRequiredService<AppDbContext>();
        Assert.Equal(3, (await db.ProductVariants.SingleAsync(x => x.Id == product.VariantId)).StockQuantity);
        Assert.Single(await db.StockMovements.Where(x => x.ReferenceId == completed.OrderId && x.MovementType == StockMovementType.Sale).ToListAsync());
        Assert.NotNull(await db.Invoices.SingleOrDefaultAsync(x => x.OrderId == completed.OrderId));
        Assert.NotEmpty(await db.NotificationDeliveries.Where(x => x.UserId == user.Id).ToListAsync());
    }

    [Fact]
    public async Task Failed_payment_does_not_change_stock()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var user = await GetCustomerAsync(services);
        var product = await CreateProductAsync(services, 4);
        await services.GetRequiredService<ICartService>().ClearAsync(new CartOwner(user.Id, null), default);
        var address = await services.GetRequiredService<IAddressService>().SaveAsync(user.Id, null,
            new AddressInput("İş", "Test", "Customer", "+905551112233", "Türkiye", "Ankara", "Çankaya", null, "06000", "Test adresi", true, true), default);
        var cart = await services.GetRequiredService<ICartService>().AddAsync(new CartOwner(user.Id, null), product.ProductId, product.VariantId, 1, default);
        var checkout = services.GetRequiredService<ICheckoutService>();
        var initialized = await checkout.InitializeAsync(new CheckoutRequest(user.Id, cart.CartId, address.Id, address.Id, Guid.NewGuid().ToString("N"), null), "https://localhost/payments/Mock/callback", default);
        var completed = await checkout.CompleteAsync("Mock", new PaymentCallbackRequest(initialized.RequestReference, $"tx-{Guid.NewGuid():N}", "fail"), default);

        Assert.Equal(PaymentStatus.Failed, completed.PaymentStatus);
        var db = services.GetRequiredService<AppDbContext>();
        Assert.Equal(4, (await db.ProductVariants.SingleAsync(x => x.Id == product.VariantId)).StockQuantity);
        Assert.Empty(await db.StockMovements.Where(x => x.ReferenceId == completed.OrderId).ToListAsync());
    }

    [Fact]
    public async Task Invoice_access_is_owner_scoped()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var user = await GetCustomerAsync(services);
        var product = await CreateProductAsync(services, 2);
        await services.GetRequiredService<ICartService>().ClearAsync(new CartOwner(user.Id, null), default);
        var address = await services.GetRequiredService<IAddressService>().SaveAsync(user.Id, null,
            new AddressInput("Ev", "Test", "Customer", "+905551112233", "Türkiye", "İzmir", "Konak", null, "35000", "Test adresi", true, true), default);
        var cart = await services.GetRequiredService<ICartService>().AddAsync(new CartOwner(user.Id, null), product.ProductId, product.VariantId, 1, default);
        var checkout = services.GetRequiredService<ICheckoutService>();
        var initialized = await checkout.InitializeAsync(new CheckoutRequest(user.Id, cart.CartId, address.Id, address.Id, Guid.NewGuid().ToString("N"), null), "https://localhost/payments/Mock/callback", default);
        var completed = await checkout.CompleteAsync("Mock", new PaymentCallbackRequest(initialized.RequestReference, $"tx-{Guid.NewGuid():N}", "success"), default);
        var db = services.GetRequiredService<AppDbContext>();
        var invoice = await db.Invoices.SingleAsync(x => x.OrderId == completed.OrderId);
        var orders = services.GetRequiredService<IOrderService>();

        Assert.NotNull(await orders.OpenInvoiceAsync(user.Id, invoice.Id, default));
        Assert.Null(await orders.OpenInvoiceAsync(Guid.NewGuid(), invoice.Id, default));
    }

    [Fact]
    public async Task Checkout_idempotency_key_creates_one_pending_order_and_payment()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var user = await GetCustomerAsync(services);
        var product = await CreateProductAsync(services, 3);
        var carts = services.GetRequiredService<ICartService>();
        await carts.ClearAsync(new CartOwner(user.Id, null), default);
        var address = await services.GetRequiredService<IAddressService>().SaveAsync(user.Id, null,
            new AddressInput("Ev", "Test", "Customer", "+905551112233", "Türkiye", "Bursa", "Nilüfer", null, "16000", "Test adresi", true, true), default);
        var cart = await carts.AddAsync(new CartOwner(user.Id, null), product.ProductId, product.VariantId, 1, default);
        var key = Guid.NewGuid().ToString("N");
        var checkout = services.GetRequiredService<ICheckoutService>();
        var first = await checkout.InitializeAsync(new CheckoutRequest(user.Id, cart.CartId, address.Id, address.Id, key, null), "https://localhost/payments/Mock/callback", default);
        var replay = await checkout.InitializeAsync(new CheckoutRequest(user.Id, cart.CartId, address.Id, address.Id, key, null), "https://localhost/payments/Mock/callback", default);

        Assert.Equal(first.OrderId, replay.OrderId);
        Assert.Equal(first.PaymentId, replay.PaymentId);
        var db = services.GetRequiredService<AppDbContext>();
        Assert.Equal(1, await db.Orders.CountAsync(x => x.UserId == user.Id && x.IdempotencyKey == key));
        Assert.Equal(1, await db.Payments.CountAsync(x => x.OrderId == first.OrderId));
    }

    [Fact]
    public async Task Verified_payment_with_exhausted_stock_is_refunded_and_cancelled()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var user = await GetCustomerAsync(services);
        var product = await CreateProductAsync(services, 1);
        var carts = services.GetRequiredService<ICartService>();
        await carts.ClearAsync(new CartOwner(user.Id, null), default);
        var address = await services.GetRequiredService<IAddressService>().SaveAsync(user.Id, null,
            new AddressInput("Ev", "Test", "Customer", "+905551112233", "Türkiye", "Antalya", "Muratpaşa", null, "07000", "Test adresi", true, true), default);
        var cart = await carts.AddAsync(new CartOwner(user.Id, null), product.ProductId, product.VariantId, 1, default);
        var checkout = services.GetRequiredService<ICheckoutService>();
        var initialized = await checkout.InitializeAsync(new CheckoutRequest(user.Id, cart.CartId, address.Id, address.Id, Guid.NewGuid().ToString("N"), null), "https://localhost/payments/Mock/callback", default);
        var db = services.GetRequiredService<AppDbContext>();
        var variant = await db.ProductVariants.SingleAsync(x => x.Id == product.VariantId);
        variant.AdjustStock(-1);
        await db.SaveChangesAsync();

        var result = await checkout.CompleteAsync("Mock", new PaymentCallbackRequest(initialized.RequestReference, $"stock-{Guid.NewGuid():N}", "success"), default);

        Assert.Equal(OrderStatus.Cancelled, result.OrderStatus);
        Assert.Equal(PaymentStatus.Refunded, result.PaymentStatus);
        Assert.Single(await db.Refunds.Where(x => x.PaymentId == initialized.PaymentId && x.Status == RefundStatus.Succeeded).ToListAsync());
        Assert.Empty(await db.StockMovements.Where(x => x.ReferenceId == initialized.OrderId && x.MovementType == StockMovementType.Sale).ToListAsync());
        Assert.Equal(0, (await db.ProductVariants.SingleAsync(x => x.Id == product.VariantId)).StockQuantity);
    }

    [Fact]
    public async Task Customer_cancellation_refunds_and_restores_stock_exactly_once()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var user = await GetCustomerAsync(services);
        var product = await CreateProductAsync(services, 2);
        var carts = services.GetRequiredService<ICartService>();
        await carts.ClearAsync(new CartOwner(user.Id, null), default);
        var address = await services.GetRequiredService<IAddressService>().SaveAsync(user.Id, null,
            new AddressInput("Ev", "Test", "Customer", "+905551112233", "Türkiye", "Samsun", "Atakum", null, "55000", "Test adresi", true, true), default);
        var cart = await carts.AddAsync(new CartOwner(user.Id, null), product.ProductId, product.VariantId, 1, default);
        var checkout = services.GetRequiredService<ICheckoutService>();
        var initialized = await checkout.InitializeAsync(new CheckoutRequest(user.Id, cart.CartId, address.Id, address.Id, Guid.NewGuid().ToString("N"), null), "https://localhost/payments/Mock/callback", default);
        var completed = await checkout.CompleteAsync("Mock", new PaymentCallbackRequest(initialized.RequestReference, $"cancel-{Guid.NewGuid():N}", "success"), default);
        var orders = services.GetRequiredService<IOrderService>();

        Assert.True((await orders.CancelAsync(user.Id, completed.OrderId, default)).Succeeded);
        Assert.False((await orders.CancelAsync(user.Id, completed.OrderId, default)).Succeeded);
        var db = services.GetRequiredService<AppDbContext>();
        Assert.Equal(2, (await db.ProductVariants.SingleAsync(x => x.Id == product.VariantId)).StockQuantity);
        Assert.Single(await db.StockMovements.Where(x => x.ReferenceId == completed.OrderId && x.MovementType == StockMovementType.Cancellation).ToListAsync());
        Assert.Single(await db.Refunds.Where(x => x.PaymentId == initialized.PaymentId && x.Status == RefundStatus.Succeeded).ToListAsync());
    }

    [Fact]
    public async Task Reused_provider_transaction_cannot_complete_a_second_order_or_stock_movement()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var user = await GetCustomerAsync(services);
        var firstProduct = await CreateProductAsync(services, 2);
        var secondProduct = await CreateProductAsync(services, 2);
        var carts = services.GetRequiredService<ICartService>();
        var address = await services.GetRequiredService<IAddressService>().SaveAsync(user.Id, null,
            new AddressInput("Ev", "Test", "Customer", "+905551112233", "Türkiye", "Eskişehir", "Tepebaşı", null, "26000", "Test adresi", true, true), default);
        await carts.ClearAsync(new CartOwner(user.Id, null), default);
        var firstCart = await carts.AddAsync(new CartOwner(user.Id, null), firstProduct.ProductId, firstProduct.VariantId, 1, default);
        var checkout = services.GetRequiredService<ICheckoutService>();
        var first = await checkout.InitializeAsync(new CheckoutRequest(user.Id, firstCart.CartId, address.Id, address.Id, Guid.NewGuid().ToString("N"), null), "https://localhost/payments/Mock/callback", default);
        await carts.ClearAsync(new CartOwner(user.Id, null), default);
        var secondCart = await carts.AddAsync(new CartOwner(user.Id, null), secondProduct.ProductId, secondProduct.VariantId, 1, default);
        var second = await checkout.InitializeAsync(new CheckoutRequest(user.Id, secondCart.CartId, address.Id, address.Id, Guid.NewGuid().ToString("N"), null), "https://localhost/payments/Mock/callback", default);
        const string transaction = "provider-transaction-reused";

        var firstResult = await checkout.CompleteAsync("Mock", new PaymentCallbackRequest(first.RequestReference, transaction, "success"), default);
        var secondResult = await checkout.CompleteAsync("Mock", new PaymentCallbackRequest(second.RequestReference, transaction, "success"), default);

        Assert.True(secondResult.IsIdempotentReplay);
        Assert.Equal(firstResult.OrderId, secondResult.OrderId);
        var db = services.GetRequiredService<AppDbContext>();
        Assert.Equal(2, (await db.ProductVariants.SingleAsync(x => x.Id == secondProduct.VariantId)).StockQuantity);
        Assert.Empty(await db.StockMovements.Where(x => x.ReferenceId == second.OrderId).ToListAsync());
        Assert.Equal(OrderStatus.PendingPayment, (await db.Orders.SingleAsync(x => x.Id == second.OrderId)).Status);
    }

    private static async Task<ApplicationUser> GetCustomerAsync(IServiceProvider services) =>
        await services.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(AeternumWebApplicationFactory.CustomerEmail)
            ?? throw new InvalidOperationException("Test customer was not found.");

    private static async Task<(Guid ProductId, Guid VariantId)> CreateProductAsync(IServiceProvider services, int stock)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;
        var token = Guid.NewGuid().ToString("N");
        var category = new Category { Name = $"Category {token}", Slug = $"category-{token}", CreatedAtUtc = now, UpdatedAtUtc = now };
        var product = new Product
        {
            Name = $"Product {token}", Slug = $"product-{token}", Sku = $"SKU-{token}", ShortDescription = "Test", Description = "Test product",
            BasePrice = 100, TaxRate = 0, Category = category, IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now,
        };
        var variant = new ProductVariant { Product = product, Weight = 250, Unit = WeightUnit.Gram, Sku = $"VAR-{token}", Price = 100, StockQuantity = stock, IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now };
        product.Variants.Add(variant);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return (product.Id, variant.Id);
    }
}
