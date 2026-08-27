using System.Net;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Infrastructure.Persistence;
using AETKAHVE.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AETKAHVE.IntegrationTests;

public sealed class AdminDashboardIntegrationTests(AeternumWebApplicationFactory factory)
    : IClassFixture<AeternumWebApplicationFactory>
{
    private static readonly string[] ModulePaths =
    [
        "/admin/products",
        "/admin/catalog",
        "/admin/orders",
        "/admin/shipments",
        "/admin/invoices",
        "/admin/returns",
        "/admin/campaigns",
        "/admin/coupons",
        "/admin/reviews",
        "/admin/messages",
        "/admin/reports",
    ];

    [Fact]
    public async Task Admin_dashboard_projects_live_commerce_data_and_connects_every_module()
    {
        var orderNumber = await SeedDashboardDataAsync();
        using var client = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(
            client,
            "/admin",
            AeternumWebApplicationFactory.AdminEmail)).StatusCode);

        using var response = await client.GetAsync("/admin");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-admin-dashboard", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Ticaret modülü devreye alındığında", html, StringComparison.Ordinal);
        Assert.Contains(orderNumber, html, StringComparison.Ordinal);
        Assert.Contains("data-metric=\"critical-stock\" data-alert=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"page\"", html, StringComparison.Ordinal);
        Assert.Contains("data-admin-nav-toggle", html, StringComparison.Ordinal);
        Assert.Contains("data-admin-sidebar", html, StringComparison.Ordinal);
        foreach (var path in ModulePaths)
        {
            Assert.Contains($"href=\"{path}\"", html, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Superadmin_dashboard_uses_only_its_own_authorization_and_security_routes()
    {
        using var client = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(
            client,
            "/superadmin",
            AeternumWebApplicationFactory.SuperAdminEmail)).StatusCode);

        using var response = await client.GetAsync("/superadmin");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-superadmin-dashboard", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/superadmin/security\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/superadmin/admins\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/admin", html, StringComparison.Ordinal);
    }

    private async Task<string> SeedDashboardDataAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = factory.Clock.GetUtcNow();
        var token = Guid.NewGuid().ToString("N");
        var category = new Category
        {
            Name = $"Dashboard category {token}",
            Slug = $"dashboard-category-{token}",
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        db.Products.Add(new Product
        {
            Name = $"Dashboard product {token}",
            Slug = $"dashboard-product-{token}",
            Sku = $"DASH-{token}",
            ShortDescription = "Dashboard test product",
            Description = "Dashboard test product",
            BasePrice = 321,
            TaxRate = 10,
            StockQuantity = 1,
            CriticalStockLevel = 5,
            Category = category,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });

        var orderNumber = $"DASH-{token[..10].ToUpperInvariant()}";
        db.Orders.Add(new Order
        {
            OrderNumber = orderNumber,
            UserId = Guid.NewGuid(),
            Status = OrderStatus.PaymentReceived,
            PaymentStatus = PaymentStatus.Succeeded,
            ShippingStatus = ShipmentStatus.Pending,
            BillingAddressSnapshot = "{}",
            ShippingAddressSnapshot = "{}",
            Subtotal = 300,
            DiscountTotal = 0,
            TaxTotal = 21,
            ShippingTotal = 0,
            GrandTotal = 321,
            Currency = "TRY",
            IdempotencyKey = $"dashboard-{token}",
            PaidAtUtc = now.AddMinutes(-5),
            CreatedAtUtc = now.AddMinutes(-10),
            UpdatedAtUtc = now.AddMinutes(-5),
        });

        await db.SaveChangesAsync();
        return orderNumber;
    }
}
