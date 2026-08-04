using System.Net;
using AETKAHVE.IntegrationTests.Infrastructure;

namespace AETKAHVE.IntegrationTests;

public sealed class FoundationSmokeTests(AeternumWebApplicationFactory factory)
    : IClassFixture<AeternumWebApplicationFactory>
{
    [Fact]
    public async Task Public_home_is_available_without_management_disclosure()
    {
        using var client = factory.CreateClientWithoutRedirects();

        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("/admin", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/superadmin", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("data-frame-manifest-url", html, StringComparison.Ordinal);
        Assert.Contains("data-poster-url", html, StringComparison.Ordinal);
        Assert.Contains("data-reduced-motion", html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/admin", "/admin/login")]
    [InlineData("/superadmin", "/superadmin/login")]
    public async Task Anonymous_management_requests_are_challenged(string path, string expectedLoginPath)
    {
        using var client = factory.CreateClientWithoutRedirects();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(expectedLoginPath, response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Customer_cannot_use_customer_login_for_management_role()
    {
        using var client = factory.CreateClientWithoutRedirects();

        var login = await FormClient.LoginAsync(client, "/account", AeternumWebApplicationFactory.AdminEmail);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.False(login.Headers.TryGetValues("Set-Cookie", out var cookies) &&
            cookies.Any(cookie => cookie.StartsWith("AETKAHVE.Customer.Auth", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Admin_and_superadmin_policy_matrix_is_enforced()
    {
        using var adminClient = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(
            adminClient,
            "/admin",
            AeternumWebApplicationFactory.AdminEmail)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync("/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await adminClient.GetAsync("/superadmin")).StatusCode);

        using var superAdminClient = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(
            superAdminClient,
            "/superadmin",
            AeternumWebApplicationFactory.SuperAdminEmail)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await superAdminClient.GetAsync("/superadmin")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await superAdminClient.GetAsync("/admin")).StatusCode);
    }

    [Fact]
    public async Task Unsafe_request_without_antiforgery_token_is_rejected()
    {
        using var client = factory.CreateClientWithoutRedirects();

        var response = await client.PostAsync("/admin/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = AeternumWebApplicationFactory.AdminEmail,
            ["Password"] = AeternumWebApplicationFactory.Password,
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
