using System.Net;
using AETKAHVE.Infrastructure.Persistence;
using AETKAHVE.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AETKAHVE.IntegrationTests;

public sealed class AfkAndAuditTests(AeternumWebApplicationFactory factory)
    : IClassFixture<AeternumWebApplicationFactory>
{
    [Fact]
    public async Task Keep_alive_extends_the_idle_session()
    {
        using var client = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(
            client,
            "/admin",
            AeternumWebApplicationFactory.AdminEmail)).StatusCode);
        var (_, token) = await FormClient.GetFormAsync(client, "/admin");

        factory.Clock.Advance(TimeSpan.FromMinutes(14));
        var keepAlive = await FormClient.PostWithTokenAsync(
            client,
            "/admin/session/keep-alive",
            token,
            new Dictionary<string, string>());
        factory.Clock.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal(HttpStatusCode.OK, keepAlive.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin")).StatusCode);
    }

    [Fact]
    public async Task Admin_session_expires_after_idle_timeout()
    {
        using var client = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(
            client,
            "/admin",
            AeternumWebApplicationFactory.AdminEmail)).StatusCode);

        factory.Clock.Advance(TimeSpan.FromMinutes(16));
        var response = await client.GetAsync("/admin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var logs = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AuditLogs.ToListAsync();
        Assert.Contains(logs, log => log.ActionType == "IdleTimeoutLogout");
    }

    [Fact]
    public async Task Idle_timeout_deletes_the_management_authentication_cookie()
    {
        using var client = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(
            client,
            "/admin",
            AeternumWebApplicationFactory.AdminEmail,
            rememberMe: true)).StatusCode);

        factory.Clock.Advance(TimeSpan.FromMinutes(16));
        var response = await client.GetAsync("/admin/session/status");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var deletionCookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("AETKAHVE.Admin.Auth=", StringComparison.Ordinal));
        Assert.Contains("expires=", deletionCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Management_logout_deletes_the_persistent_authentication_cookie()
    {
        using var client = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(
            client,
            "/admin",
            AeternumWebApplicationFactory.AdminEmail,
            rememberMe: true)).StatusCode);
        var (_, token) = await FormClient.GetFormAsync(client, "/admin");

        var response = await FormClient.PostWithTokenAsync(
            client,
            "/admin/logout",
            token,
            new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var deletionCookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("AETKAHVE.Admin.Auth=", StringComparison.Ordinal));
        Assert.Contains("expires=", deletionCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Status_poll_does_not_extend_idle_session()
    {
        using var client = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(
            client,
            "/admin",
            AeternumWebApplicationFactory.AdminEmail)).StatusCode);

        factory.Clock.Advance(TimeSpan.FromMinutes(14));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/admin/session/status")).StatusCode);
        factory.Clock.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/admin")).StatusCode);
    }
}
