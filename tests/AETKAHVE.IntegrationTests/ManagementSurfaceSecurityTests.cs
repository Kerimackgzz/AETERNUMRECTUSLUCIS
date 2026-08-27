using System.Net;
using System.Net.Http.Json;
using AETKAHVE.IntegrationTests.Infrastructure;

namespace AETKAHVE.IntegrationTests;

public sealed class ManagementSurfaceSecurityTests(AeternumWebApplicationFactory factory)
    : IClassFixture<AeternumWebApplicationFactory>
{
    private static readonly string[] AdminPages =
    [
        "/admin/catalog",
        "/admin/products",
        "/admin/orders",
        "/admin/invoices",
        "/admin/shipments",
        "/admin/campaigns",
        "/admin/coupons",
        "/admin/returns",
        "/admin/reviews",
        "/admin/messages",
        "/admin/reports",
    ];

    [Fact]
    public async Task Admin_pages_require_the_management_policy_and_emit_security_headers()
    {
        using var anonymousClient = factory.CreateClientWithoutRedirects();
        foreach (var path in AdminPages)
        {
            var anonymousResponse = await anonymousClient.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, anonymousResponse.StatusCode);
            Assert.Equal("/admin/login", anonymousResponse.Headers.Location?.AbsolutePath);
        }

        using var adminClient = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(
            adminClient,
            "/admin",
            AeternumWebApplicationFactory.AdminEmail)).StatusCode);

        foreach (var path in AdminPages)
        {
            var response = await adminClient.GetAsync(path);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("no-store", response.Headers.CacheControl?.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
            Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
            Assert.True(response.Headers.Contains("Content-Security-Policy"));
            Assert.True(response.Headers.Contains("X-Correlation-ID"));
        }
    }

    [Fact]
    public async Task Superadmin_and_customer_cannot_use_admin_commerce_pages()
    {
        using var superAdminClient = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(
            superAdminClient,
            "/superadmin",
            AeternumWebApplicationFactory.SuperAdminEmail)).StatusCode);
        var superAdminResponse = await superAdminClient.GetAsync("/admin/products");
        Assert.Equal(HttpStatusCode.Redirect, superAdminResponse.StatusCode);
        Assert.Equal("/admin/login", superAdminResponse.Headers.Location?.AbsolutePath);

        using var customerClient = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(
            customerClient,
            "/account",
            AeternumWebApplicationFactory.CustomerEmail)).StatusCode);

        var customerResponse = await customerClient.GetAsync("/admin/products");
        Assert.Equal(HttpStatusCode.Redirect, customerResponse.StatusCode);
        Assert.Equal("/admin/login", customerResponse.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Authenticated_admin_json_mutations_still_require_antiforgery()
    {
        using var client = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(
            client,
            "/admin",
            AeternumWebApplicationFactory.AdminEmail)).StatusCode);

        var response = await client.PostAsJsonAsync("/admin/products", new
        {
            name = "Antiforgery probe",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
