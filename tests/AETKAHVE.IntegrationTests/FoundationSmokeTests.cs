using System.Net;
using AETKAHVE.Application.Security;
using AETKAHVE.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

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
        var adminArea = await superAdminClient.GetAsync("/admin");
        Assert.Equal(HttpStatusCode.Redirect, adminArea.StatusCode);
        Assert.Equal("/admin/login", adminArea.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task Switching_management_portals_keeps_the_other_portal_session_active()
    {
        // Different real-world accounts routinely need an Admin session and a SuperAdmin
        // session open in the same browser at once (e.g. two staff members' credentials
        // used from the same machine). Logging into one portal must not silently sign the
        // other one out.
        using var client = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(
            client,
            "/admin",
            AeternumWebApplicationFactory.AdminEmail)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin")).StatusCode);

        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(
            client,
            "/superadmin",
            AeternumWebApplicationFactory.SuperAdminEmail)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/superadmin")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/superadmin")).StatusCode);
    }

    [Fact]
    public async Task Overlapping_management_cookies_can_open_both_portals()
    {
        // Each portal authenticates strictly against its own named cookie/scheme
        // (AuthenticationSchemes.Admin / .SuperAdmin), so the mere presence of the other
        // portal's cookie in the same browser must not block access to either one.
        using var adminClient = factory.CreateClientWithoutRedirects();
        using var adminLogin = await FormClient.LoginAsync(
            adminClient,
            "/admin",
            AeternumWebApplicationFactory.AdminEmail);
        var adminCookie = AuthenticationCookie(adminLogin, CookieNames.Admin);

        using var superAdminClient = factory.CreateClientWithoutRedirects();
        using var superAdminLogin = await FormClient.LoginAsync(
            superAdminClient,
            "/superadmin",
            AeternumWebApplicationFactory.SuperAdminEmail);
        var superAdminCookie = AuthenticationCookie(superAdminLogin, CookieNames.SuperAdmin);

        using var overlappingClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
        });
        overlappingClient.DefaultRequestHeaders.Add("Cookie", $"{adminCookie}; {superAdminCookie}");

        Assert.Equal(HttpStatusCode.OK, (await overlappingClient.GetAsync("/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await overlappingClient.GetAsync("/superadmin")).StatusCode);
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

    private static string AuthenticationCookie(HttpResponseMessage response, string cookieName) =>
        Assert.Single(
                response.Headers.GetValues("Set-Cookie"),
                value => value.StartsWith(cookieName + "=", StringComparison.Ordinal))
            .Split(';', 2)[0];
}
