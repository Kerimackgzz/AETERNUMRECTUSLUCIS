using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using AETKAHVE.Application.Commerce;
using AETKAHVE.Domain.Common;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Infrastructure.Identity;
using AETKAHVE.Infrastructure.Persistence;
using AETKAHVE.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AETKAHVE.IntegrationTests;

public sealed class CommerceHardeningTests(AeternumWebApplicationFactory factory)
    : IClassFixture<AeternumWebApplicationFactory>
{
    [Fact]
    public async Task Campaigns_is_an_authenticated_commerce_entry_point_that_merges_the_guest_cart()
    {
        Guid userId;
        (Guid ProductId, Guid VariantId) product;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            userId = (await GetUserAsync(services, AeternumWebApplicationFactory.CustomerEmail)).Id;
            await services.GetRequiredService<ICartService>().ClearAsync(new CartOwner(userId, null), default);
            product = await CreateProductAsync(services, 4);
        }

        using var client = factory.CreateClientWithoutRedirects();
        var token = await GetAntiforgeryTokenAsync(client, "/products");
        using (var add = await PostJsonAsync(client, "/cart/items", token,
                   new { productId = product.ProductId, variantId = product.VariantId, quantity = 2 }))
        {
            Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        }

        await LoginAsync(client, AeternumWebApplicationFactory.CustomerEmail);
        using var campaigns = await client.GetAsync("/campaigns");
        Assert.Equal(HttpStatusCode.OK, campaigns.StatusCode);

        await using var assertionScope = factory.Services.CreateAsyncScope();
        var db = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userCart = await db.Carts.Include(x => x.Items).SingleAsync(x => x.UserId == userId);
        Assert.Equal(2, Assert.Single(userCart.Items, x => x.ProductId == product.ProductId).Quantity);
        Assert.False(await db.Carts.AnyAsync(x => x.GuestToken != null && x.Items.Any(i => i.ProductId == product.ProductId)));
    }

    [Fact]
    public async Task Guest_cart_merge_removes_a_coupon_that_became_invalid_without_bricking_the_user_cart()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var user = await GetUserAsync(services, AeternumWebApplicationFactory.CustomerEmail);
        var carts = services.GetRequiredService<ICartService>();
        await carts.ClearAsync(new CartOwner(user.Id, null), default);
        var product = await CreateProductAsync(services, 3);
        var guestToken = Guid.NewGuid();
        await carts.AddAsync(new CartOwner(null, guestToken), product.ProductId, product.VariantId, 1, default);

        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var coupon = new Coupon
        {
            Name = "Merge coupon",
            Code = $"MERGE-{Guid.NewGuid():N}".ToUpperInvariant(),
            DiscountType = DiscountType.FixedAmount,
            DiscountValue = 10,
            StartDateUtc = now.AddDays(-1),
            EndDateUtc = now.AddDays(1),
            IsActive = true,
            CanCombineWithOtherDiscounts = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        var db = services.GetRequiredService<AppDbContext>();
        db.Coupons.Add(coupon);
        await db.SaveChangesAsync();
        await carts.ApplyCouponAsync(new CartOwner(null, guestToken), coupon.Code, default);

        coupon.IsActive = false;
        coupon.UpdatedAtUtc = now.AddMinutes(1);
        await db.SaveChangesAsync();

        var merged = await carts.MergeGuestCartAsync(user.Id, guestToken, default);

        Assert.Null(merged.Cart.CouponCode);
        Assert.Single(merged.Cart.Items);
        Assert.Contains(merged.Warnings, x => x.Contains("coupon", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(merged.Cart.Warnings, x => x.Contains("Coupon was removed", StringComparison.Ordinal));
        Assert.False(await db.Carts.AnyAsync(x => x.GuestToken == guestToken));
        Assert.Null((await db.Carts.SingleAsync(x => x.UserId == user.Id)).CouponCode);
    }

    [Fact]
    public async Task Guest_cart_merge_discards_lines_that_became_out_of_stock()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var user = await GetUserAsync(services, AeternumWebApplicationFactory.CustomerEmail);
        var carts = services.GetRequiredService<ICartService>();
        await carts.ClearAsync(new CartOwner(user.Id, null), default);
        var product = await CreateProductAsync(services, 1);
        var guestToken = Guid.NewGuid();
        await carts.AddAsync(new CartOwner(null, guestToken), product.ProductId, product.VariantId, 1, default);

        var db = services.GetRequiredService<AppDbContext>();
        var variant = await db.ProductVariants.SingleAsync(x => x.Id == product.VariantId);
        variant.AdjustStock(-1);
        await db.SaveChangesAsync();

        var merged = await carts.MergeGuestCartAsync(user.Id, guestToken, default);

        Assert.Empty(merged.Cart.Items);
        Assert.NotEmpty(merged.Warnings);
        Assert.False(await db.CartItems.AnyAsync(x => x.Cart.UserId == user.Id && x.ProductId == product.ProductId));
    }

    [Fact]
    public async Task Duplicate_return_lines_cannot_exceed_the_purchased_quantity_or_refund_amount()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var user = await GetUserAsync(services, AeternumWebApplicationFactory.CustomerEmail);
        var purchase = await CreateDeliveredPurchaseAsync(services, user.Id, 1, includeInvoice: false);
        var returns = services.GetRequiredService<IReturnService>();
        var line = new ReturnItemInput(purchase.OrderItemId, 1, "Duplicate", ReturnItemCondition.Unopened, null);

        var exception = await Assert.ThrowsAsync<CommerceRuleException>(() => returns.CreateAsync(
            new ReturnCreateRequest(user.Id, purchase.OrderId, "Duplicate lines", null, [line, line]), default));

        Assert.Contains("only once", exception.Message, StringComparison.Ordinal);
        var db = services.GetRequiredService<AppDbContext>();
        Assert.False(await db.ReturnRequests.AnyAsync(x => x.OrderId == purchase.OrderId));
        Assert.Equal(OrderStatus.Delivered, (await db.Orders.SingleAsync(x => x.Id == purchase.OrderId)).Status);
    }

    [Fact]
    public async Task Malformed_payment_callbacks_return_bad_request_instead_of_server_error()
    {
        using var client = factory.CreateClientWithoutRedirects();

        using var missingStatus = await client.PostAsync("/payments/Mock/callback",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["reference"] = "some-reference" }));
        Assert.Equal(HttpStatusCode.BadRequest, missingStatus.StatusCode);

        using var missingReference = await client.GetAsync("/payments/Mock/callback?status=success");
        Assert.Equal(HttpStatusCode.BadRequest, missingReference.StatusCode);

        using var oversizedReference = await client.GetAsync($"/payments/Mock/callback?reference={new string('x', 161)}&status=success");
        Assert.Equal(HttpStatusCode.BadRequest, oversizedReference.StatusCode);
    }

    [Fact]
    public async Task Address_json_updates_are_antiforgery_protected_and_owner_scoped()
    {
        Guid foreignAddressId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var other = await GetUserAsync(services, AeternumWebApplicationFactory.ResetEmail);
            var address = await services.GetRequiredService<IAddressService>().SaveAsync(other.Id, null,
                new AddressInput("Foreign", "Other", "Customer", "+905551112233", "Türkiye", "Ankara", "Çankaya", null,
                    "06000", "Foreign owner address", true, true), default);
            foreignAddressId = address.Id;
        }

        using var client = factory.CreateClientWithoutRedirects();
        await LoginAsync(client, AeternumWebApplicationFactory.CustomerEmail);
        var body = new
        {
            id = foreignAddressId,
            title = "Hijacked",
            firstName = "Main",
            lastName = "Customer",
            phoneNumber = "+905559998877",
            country = "Türkiye",
            city = "İstanbul",
            district = "Kadıköy",
            neighborhood = (string?)null,
            postalCode = "34000",
            addressLine = "Must not overwrite",
            isDefaultShipping = true,
            isDefaultBilling = true,
        };

        using (var noToken = await client.PostAsJsonAsync("/account/addresses", body))
            Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);

        var token = await GetAntiforgeryTokenAsync(client, "/account/addresses");
        using (var foreignUpdate = await PostJsonAsync(client, "/account/addresses", token, body))
            Assert.Equal(HttpStatusCode.Conflict, foreignUpdate.StatusCode);

        await using var assertionScope = factory.Services.CreateAsyncScope();
        var db = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var unchanged = await db.Addresses.SingleAsync(x => x.Id == foreignAddressId);
        Assert.Equal("Foreign", unchanged.Title);
        Assert.Equal("Foreign owner address", unchanged.AddressLine);
    }

    [Fact]
    public async Task Customer_order_invoice_return_review_and_cancel_endpoints_do_not_expose_foreign_records()
    {
        Guid currentUserId;
        (Guid OrderId, Guid OrderItemId, Guid? InvoiceId) foreign;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            currentUserId = (await GetUserAsync(services, AeternumWebApplicationFactory.CustomerEmail)).Id;
            var other = await GetUserAsync(services, AeternumWebApplicationFactory.ResetEmail);
            foreign = await CreateDeliveredPurchaseAsync(services, other.Id, 1, includeInvoice: true);
        }

        using var client = factory.CreateClientWithoutRedirects();
        await LoginAsync(client, AeternumWebApplicationFactory.CustomerEmail);
        var token = await GetAntiforgeryTokenAsync(client, "/account/orders");

        using (var order = await client.GetAsync($"/account/orders/{foreign.OrderId}"))
            Assert.Equal(HttpStatusCode.NotFound, order.StatusCode);
        using (var invoice = await client.GetAsync($"/account/invoices/{foreign.InvoiceId}/download"))
            Assert.Equal(HttpStatusCode.NotFound, invoice.StatusCode);
        using (var cancel = await PostWithoutBodyAsync(client, $"/account/orders/{foreign.OrderId}/cancel", token))
            Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
        using (var returnRequest = await PostJsonAsync(client, "/account/returns", token, new
               {
                   orderId = foreign.OrderId,
                   reason = "Ownership probe",
                   description = "Must fail",
                   items = new[] { new { orderItemId = foreign.OrderItemId, quantity = 1, reason = "Probe", condition = 0, imageStorageKey = (string?)null } },
               }))
            Assert.Equal(HttpStatusCode.Conflict, returnRequest.StatusCode);
        using (var review = await PostJsonAsync(client, "/account/reviews", token,
                   new { orderItemId = foreign.OrderItemId, rating = 5, comment = "Ownership probe" }))
            Assert.Equal(HttpStatusCode.Conflict, review.StatusCode);

        await using var assertionScope = factory.Services.CreateAsyncScope();
        var db = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.ReturnRequests.AnyAsync(x => x.UserId == currentUserId && x.OrderId == foreign.OrderId));
        Assert.False(await db.Reviews.AnyAsync(x => x.UserId == currentUserId && x.OrderItemId == foreign.OrderItemId));
    }

    [Fact]
    public async Task Checkout_json_rejects_foreign_cart_and_address_ownership()
    {
        Guid currentUserId;
        Guid foreignAddressId;
        Guid foreignCartId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            var current = await GetUserAsync(services, AeternumWebApplicationFactory.CustomerEmail);
            var other = await GetUserAsync(services, AeternumWebApplicationFactory.ResetEmail);
            currentUserId = current.Id;
            var carts = services.GetRequiredService<ICartService>();
            await carts.ClearAsync(new CartOwner(current.Id, null), default);
            await carts.ClearAsync(new CartOwner(other.Id, null), default);
            var product = await CreateProductAsync(services, 5);
            foreignCartId = (await carts.AddAsync(new CartOwner(other.Id, null), product.ProductId, product.VariantId, 1, default)).CartId;
            foreignAddressId = (await services.GetRequiredService<IAddressService>().SaveAsync(other.Id, null,
                new AddressInput("Other", "Other", "Customer", "+905551112233", "Türkiye", "İzmir", "Konak", null,
                    "35000", "Other address", true, true), default)).Id;
        }

        using var client = factory.CreateClientWithoutRedirects();
        await LoginAsync(client, AeternumWebApplicationFactory.CustomerEmail);
        var token = await GetAntiforgeryTokenAsync(client, "/checkout");
        var firstKey = Guid.NewGuid().ToString("N");
        using (var foreignCart = await PostJsonAsync(client, "/checkout", token, new
               {
                   cartId = foreignCartId,
                   shippingAddressId = foreignAddressId,
                   billingAddressId = foreignAddressId,
                   idempotencyKey = firstKey,
                   paymentScenario = "success",
               }))
            Assert.Equal(HttpStatusCode.Conflict, foreignCart.StatusCode);

        await using var assertionScope = factory.Services.CreateAsyncScope();
        var db = assertionScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Orders.AnyAsync(x => x.UserId == currentUserId && x.IdempotencyKey == firstKey));
    }

    [Fact]
    public async Task Contact_json_requires_antiforgery_and_rejects_invalid_email_without_persistence()
    {
        using var client = factory.CreateClientWithoutRedirects();
        var validEmail = $"no-token-{Guid.NewGuid():N}@test.local";
        var valid = new
        {
            fullName = "No Token",
            email = validEmail,
            phoneNumber = "+905551112233",
            subject = "Antiforgery",
            message = "This request must not persist.",
            privacyAccepted = true,
        };
        using (var noToken = await client.PostAsJsonAsync("/contact", valid))
            Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);

        var token = await GetAntiforgeryTokenAsync(client, "/contact");
        using (var invalidEmail = await PostJsonAsync(client, "/contact", token, new
               {
                   fullName = "Invalid Email",
                   email = "not-an-email",
                   phoneNumber = (string?)null,
                   subject = "Validation",
                   message = "This request must not persist either.",
                   privacyAccepted = true,
               }))
            Assert.Equal(HttpStatusCode.BadRequest, invalidEmail.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.ContactMessages.AnyAsync(x => x.Email == validEmail || x.Email == "not-an-email"));
    }

    [Fact]
    public async Task Live_catalog_dependents_have_query_filters_matching_soft_deleted_catalog_principals()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var model = scope.ServiceProvider.GetRequiredService<AppDbContext>().Model;
        Type[] filteredDependents =
        [
            typeof(CampaignCategory), typeof(CampaignProduct), typeof(CartItem), typeof(Favorite),
            typeof(ProductImage),
        ];

        foreach (var dependent in filteredDependents)
        {
            var entityType = model.FindEntityType(dependent);
            Assert.NotNull(entityType);
            Assert.NotEmpty(entityType.GetDeclaredQueryFilters());
        }
    }

    [Fact]
    public async Task Stock_movement_audit_rows_remain_queryable_after_product_soft_delete()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var productIds = await CreateProductAsync(services, 2);
        var db = services.GetRequiredService<AppDbContext>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var movement = new StockMovement
        {
            ProductId = productIds.ProductId,
            ProductVariantId = productIds.VariantId,
            MovementType = StockMovementType.Correction,
            Quantity = 1,
            PreviousStock = 1,
            NewStock = 2,
            ReferenceType = "HardeningAudit",
            ReferenceId = Guid.NewGuid(),
            Description = "Historical stock movement must remain visible.",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        db.StockMovements.Add(movement);
        await db.SaveChangesAsync();

        var product = await db.Products.SingleAsync(x => x.Id == productIds.ProductId);
        product.DeletedAtUtc = now;
        product.UpdatedAtUtc = now;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        Assert.False(await db.Products.AnyAsync(x => x.Id == productIds.ProductId));
        Assert.True(await db.StockMovements.AnyAsync(x => x.Id == movement.Id));
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        using var response = await FormClient.LoginAsync(client, "/account", email);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(html, "<meta name=\"csrf-token\" content=\"([^\"]+)\"", RegexOptions.IgnoreCase);
        Assert.True(match.Success, $"Commerce antiforgery meta token was not rendered at {path}.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static async Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string path, string token, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("RequestVerificationToken", token);
        var response = await client.SendAsync(request);
        request.Dispose();
        return response;
    }

    private static async Task<HttpResponseMessage> PostWithoutBodyAsync(HttpClient client, string path, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add("RequestVerificationToken", token);
        var response = await client.SendAsync(request);
        request.Dispose();
        return response;
    }

    private static async Task<ApplicationUser> GetUserAsync(IServiceProvider services, string email) =>
        await services.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email)
        ?? throw new InvalidOperationException($"Test user {email} was not found.");

    private static async Task<(Guid ProductId, Guid VariantId)> CreateProductAsync(IServiceProvider services, int stock)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var token = Guid.NewGuid().ToString("N");
        var category = new Category
        {
            Name = $"Hardening category {token}",
            Slug = $"hardening-category-{token}",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        var product = new Product
        {
            Name = $"Hardening product {token}",
            Slug = $"hardening-product-{token}",
            Sku = $"HARD-{token.ToUpperInvariant()}",
            ShortDescription = "Hardening test",
            Description = "Hardening test product",
            BasePrice = 100,
            TaxRate = 0,
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
            Sku = $"HARD-VAR-{token.ToUpperInvariant()}",
            Price = 100,
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

    private static async Task<(Guid OrderId, Guid OrderItemId, Guid? InvoiceId)> CreateDeliveredPurchaseAsync(
        IServiceProvider services,
        Guid userId,
        int quantity,
        bool includeInvoice)
    {
        var product = await CreateProductAsync(services, quantity);
        var db = services.GetRequiredService<AppDbContext>();
        var now = services.GetRequiredService<TimeProvider>().GetUtcNow();
        var token = Guid.NewGuid().ToString("N");
        var order = new Order
        {
            OrderNumber = $"HARDEN-{token}",
            UserId = userId,
            Status = OrderStatus.Delivered,
            PaymentStatus = PaymentStatus.Succeeded,
            ShippingStatus = ShipmentStatus.Delivered,
            BillingAddressSnapshot = "{}",
            ShippingAddressSnapshot = "{}",
            Subtotal = 100 * quantity,
            GrandTotal = 100 * quantity,
            IdempotencyKey = token,
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
            ProductName = "Hardening delivered product",
            Sku = $"HARD-ITEM-{token.ToUpperInvariant()}",
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
            TransactionId = $"hardening-{token}",
            IdempotencyKey = token,
            Amount = order.GrandTotal,
            Status = PaymentStatus.Succeeded,
            CompletedAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        Invoice? invoice = null;
        if (includeInvoice)
        {
            invoice = new Invoice
            {
                Order = order,
                InvoiceNumber = $"HARD-INV-{token}",
                InvoiceDateUtc = now,
                StorageKey = $"missing-{token}.pdf",
                GrandTotal = order.GrandTotal,
                Currency = order.Currency,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            order.Invoice = invoice;
            db.Invoices.Add(invoice);
        }

        db.Orders.Add(order);
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        return (order.Id, item.Id, invoice?.Id);
    }
}
