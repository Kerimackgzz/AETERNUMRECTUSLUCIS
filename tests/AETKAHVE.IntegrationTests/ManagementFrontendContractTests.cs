using System.Net;
using AETKAHVE.IntegrationTests.Infrastructure;

namespace AETKAHVE.IntegrationTests;

public sealed class ManagementFrontendContractTests(AeternumWebApplicationFactory factory)
    : IClassFixture<AeternumWebApplicationFactory>
{
    [Theory]
    [InlineData("admin", AeternumWebApplicationFactory.AdminEmail)]
    [InlineData("superadmin", AeternumWebApplicationFactory.SuperAdminEmail)]
    public async Task Management_layout_renders_the_idle_session_contract(string portal, string email)
    {
        using var client = factory.CreateClientWithoutRedirects();
        var login = await FormClient.LoginAsync(client, $"/{portal}", email);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var response = await client.GetAsync($"/{portal}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"data-idle-session=\"{portal}\"", html, StringComparison.Ordinal);
        Assert.Contains($"data-session-status-url=\"/{portal}/session/status\"", html, StringComparison.Ordinal);
        Assert.Contains($"data-session-keep-alive-url=\"/{portal}/session/keep-alive\"", html, StringComparison.Ordinal);
        Assert.Contains($"data-session-logout-url=\"/{portal}/logout\"", html, StringComparison.Ordinal);
        Assert.Contains("meta name=\"csrf-token\"", html, StringComparison.Ordinal);
        Assert.Contains("/js/admin/idle-session.js", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Idle_session_runtime_is_served_with_logout_and_cross_tab_guards()
    {
        using var client = factory.CreateClientWithoutRedirects();

        var response = await client.GetAsync("/js/admin/idle-session.js");
        var source = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("RequestVerificationToken", source, StringComparison.Ordinal);
        Assert.Contains("credentials: \"same-origin\"", source, StringComparison.Ordinal);
        Assert.Contains("new window.BroadcastChannel", source, StringComparison.Ordinal);
        Assert.Contains("window.localStorage.setItem", source, StringComparison.Ordinal);
        Assert.Contains("logoutRequestStarted", source, StringComparison.Ordinal);
    }
}
