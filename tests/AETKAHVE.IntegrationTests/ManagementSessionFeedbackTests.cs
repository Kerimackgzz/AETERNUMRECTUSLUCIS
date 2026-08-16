using System.Net;
using AETKAHVE.Application.Security;
using AETKAHVE.Infrastructure.Identity;
using AETKAHVE.Infrastructure.Persistence;
using AETKAHVE.Infrastructure.Security;
using AETKAHVE.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AETKAHVE.IntegrationTests;

public sealed class ManagementSessionFeedbackTests(AeternumWebApplicationFactory factory)
    : IClassFixture<AeternumWebApplicationFactory>
{
    [Theory]
    [InlineData("admin", "expired", "Oturumunuz hareketsizlik nedeniyle sona erdi.")]
    [InlineData("superadmin", "session-ended", "Oturumunuz sona erdi.")]
    [InlineData("admin", "credentials-changed", "Güvenlik bilgileriniz değiştiği için tüm oturumlar kapatıldı.")]
    public async Task Allowlisted_login_reason_renders_a_single_safe_flash(
        string portal,
        string reason,
        string expectedMessage)
    {
        using var client = factory.CreateClientWithoutRedirects();

        var response = await client.GetAsync($"/{portal}/login?reason={reason}");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expectedMessage, html, StringComparison.Ordinal);
        Assert.Contains("data-server-flash-kind=\"info\"", html, StringComparison.Ordinal);

        var nextResponse = await client.GetAsync($"/{portal}/login");
        var nextHtml = await nextResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(expectedMessage, nextHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unknown_login_reason_is_not_reflected()
    {
        using var client = factory.CreateClientWithoutRedirects();

        var response = await client.GetAsync("/admin/login?reason=untrusted-message");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("untrusted-message", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-server-flash-region", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Explicit_logout_shows_a_one_shot_success_flash()
    {
        using var client = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(
            client,
            "/admin",
            AeternumWebApplicationFactory.AdminEmail)).StatusCode);
        var (_, token) = await FormClient.GetFormAsync(client, "/admin");

        var logout = await FormClient.PostWithTokenAsync(
            client,
            "/admin/logout",
            token,
            new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal("/admin/login", logout.Headers.Location?.OriginalString);

        var login = await client.GetAsync(logout.Headers.Location);
        var html = WebUtility.HtmlDecode(await login.Content.ReadAsStringAsync());
        Assert.Contains("Güvenli çıkış yapıldı.", html, StringComparison.Ordinal);
        Assert.Contains("data-server-flash-kind=\"success\"", html, StringComparison.Ordinal);

        var nextHtml = await (await client.GetAsync("/admin/login")).Content.ReadAsStringAsync();
        Assert.DoesNotContain("Güvenli çıkış yapıldı.", nextHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Idle_cookie_rejection_redirects_with_an_allowlisted_reason()
    {
        using var client = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(
            client,
            "/admin",
            AeternumWebApplicationFactory.AdminEmail)).StatusCode);

        factory.Clock.Advance(TimeSpan.FromMinutes(16));
        var response = await client.GetAsync("/admin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("expired", GetQueryValue(response.Headers.Location, "reason"));
    }

    [Fact]
    public async Task Credential_change_rejects_cookie_with_a_safe_reason()
    {
        var email = $"management-session-{Guid.NewGuid():N}@test.local";
        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            var userManager = setupScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = "Session",
                LastName = "Test",
                CreatedAtUtc = factory.Clock.GetUtcNow(),
                IsActive = true,
            };
            Assert.True((await userManager.CreateAsync(user, AeternumWebApplicationFactory.Password)).Succeeded);
            Assert.True((await userManager.AddToRoleAsync(user, RoleNames.Admin)).Succeeded);
        }

        using var client = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(client, "/admin", email)).StatusCode);

        await using (var mutationScope = factory.Services.CreateAsyncScope())
        {
            var userManager = mutationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = Assert.IsType<ApplicationUser>(await userManager.FindByEmailAsync(email));
            Assert.True((await userManager.UpdateSecurityStampAsync(user)).Succeeded);
        }

        var response = await client.GetAsync("/admin");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("credentials-changed", GetQueryValue(response.Headers.Location, "reason"));
    }

    [Fact]
    public async Task Revoke_all_closes_every_active_management_session_for_the_user()
    {
        var email = $"management-revoke-{Guid.NewGuid():N}@test.local";
        Guid userId;
        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            var userManager = setupScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = "Revoke",
                LastName = "Test",
                CreatedAtUtc = factory.Clock.GetUtcNow(),
                IsActive = true,
            };
            Assert.True((await userManager.CreateAsync(user, AeternumWebApplicationFactory.Password)).Succeeded);
            Assert.True((await userManager.AddToRolesAsync(user, [RoleNames.Admin, RoleNames.SuperAdmin])).Succeeded);
            userId = user.Id;

            var sessions = setupScope.ServiceProvider.GetRequiredService<ManagementSessionService>();
            await sessions.CreateAsync(user, AuthenticationPortal.Admin);
            await sessions.CreateAsync(user, AuthenticationPortal.SuperAdmin);
        }

        await using (var revokeScope = factory.Services.CreateAsyncScope())
        {
            var sessions = revokeScope.ServiceProvider.GetRequiredService<ManagementSessionService>();
            await sessions.RevokeAllAsync(userId, "CredentialsChanged");
        }

        await using var assertScope = factory.Services.CreateAsyncScope();
        var records = await assertScope.ServiceProvider.GetRequiredService<AppDbContext>()
            .ManagementSessions
            .Where(x => x.UserId == userId)
            .ToListAsync();
        Assert.Equal(2, records.Count);
        Assert.All(records, session =>
        {
            Assert.NotNull(session.RevokedAtUtc);
            Assert.Equal("CredentialsChanged", session.RevocationReason);
        });
    }

    [Fact]
    public async Task Sign_out_all_management_revokes_sessions_and_deletes_both_portal_cookies()
    {
        var email = $"management-signout-{Guid.NewGuid():N}@test.local";
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = "Signout",
            LastName = "Test",
            CreatedAtUtc = factory.Clock.GetUtcNow(),
            IsActive = true,
        };
        Assert.True((await userManager.CreateAsync(user, AeternumWebApplicationFactory.Password)).Succeeded);
        Assert.True((await userManager.AddToRolesAsync(user, [RoleNames.Admin, RoleNames.SuperAdmin])).Succeeded);

        var sessions = scope.ServiceProvider.GetRequiredService<ManagementSessionService>();
        await sessions.CreateAsync(user, AuthenticationPortal.Admin);
        await sessions.CreateAsync(user, AuthenticationPortal.SuperAdmin);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
        };
        httpContext.Request.Scheme = "https";
        var authenticationSessions = scope.ServiceProvider.GetRequiredService<AuthenticationSessionService>();

        await authenticationSessions.SignOutAllManagementAsync(
            httpContext,
            user.Id,
            "CredentialsChanged");

        var deletionCookies = httpContext.Response.Headers.SetCookie.ToArray();
        Assert.Contains(deletionCookies, value =>
            value is not null &&
            value.StartsWith("AETKAHVE.Admin.Auth=", StringComparison.Ordinal) &&
            value.Contains("expires=", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(deletionCookies, value =>
            value is not null &&
            value.StartsWith("AETKAHVE.SuperAdmin.Auth=", StringComparison.Ordinal) &&
            value.Contains("expires=", StringComparison.OrdinalIgnoreCase));

        var activeSessionCount = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .ManagementSessions
            .CountAsync(x => x.UserId == user.Id && !x.RevokedAtUtc.HasValue);
        Assert.Equal(0, activeSessionCount);
    }

    private static string? GetQueryValue(Uri? location, string key)
    {
        if (location is null)
        {
            return null;
        }

        var query = location.OriginalString.Split('?', 2).ElementAtOrDefault(1);
        if (query is null)
        {
            return null;
        }

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (Uri.UnescapeDataString(parts[0]).Equals(key, StringComparison.Ordinal))
            {
                return parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : string.Empty;
            }
        }

        return null;
    }
}
