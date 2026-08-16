using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AETKAHVE.Application.Security;
using AETKAHVE.Infrastructure.Commerce;
using AETKAHVE.Infrastructure.Identity;
using AETKAHVE.Infrastructure.Notifications;
using AETKAHVE.Infrastructure.Persistence;
using AETKAHVE.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AETKAHVE.IntegrationTests;

public sealed class ManagementSecuritySurfaceTests(AeternumWebApplicationFactory factory)
    : IClassFixture<AeternumWebApplicationFactory>
{
    [Theory]
    [InlineData("admin")]
    [InlineData("superadmin")]
    public async Task Security_center_is_policy_protected_and_renders_dual_roles_and_accessible_password_fields(string portal)
    {
        var email = $"security-surface-{portal}-{Guid.NewGuid():N}@test.local";
        await ManagementSecurityTestSupport.CreateDualRoleUserAsync(factory, email);

        using var anonymousClient = factory.CreateClientWithoutRedirects();
        var anonymous = await anonymousClient.GetAsync($"/{portal}/security");
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.Equal($"/{portal}/login", anonymous.Headers.Location?.AbsolutePath);

        using var client = factory.CreateClientWithoutRedirects();
        Assert.Equal(
            HttpStatusCode.Redirect,
            (await FormClient.LoginAsync(client, $"/{portal}", email)).StatusCode);

        var response = await client.GetAsync($"/{portal}/security");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Güvenlik merkezi", html, StringComparison.Ordinal);
        Assert.Contains(RoleNames.Admin, html, StringComparison.Ordinal);
        Assert.Contains(RoleNames.SuperAdmin, html, StringComparison.Ordinal);
        Assert.Contains($"action=\"/{portal}/security/email-change\"", html, StringComparison.Ordinal);
        Assert.Contains($"action=\"/{portal}/security/password\"", html, StringComparison.Ordinal);
        Assert.Equal(4, ManagementSecurityTestSupport.CountOccurrences(html, "data-password-field"));
        Assert.Equal(4, ManagementSecurityTestSupport.CountOccurrences(html, "data-password-toggle"));
        Assert.Contains("/css/admin/management-security.css", html, StringComparison.Ordinal);

        using var missingAntiforgery = await client.PostAsync(
            $"/{portal}/security/password",
            new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.BadRequest, missingAntiforgery.StatusCode);
    }
}

public sealed class ManagementSecurityEmailChangeTests(AeternumWebApplicationFactory factory)
    : IClassFixture<AeternumWebApplicationFactory>
{
    [Fact]
    public async Task Admin_email_change_works_from_an_anonymous_client_and_invalidates_both_portals()
    {
        var email = $"security-email-{Guid.NewGuid():N}@test.local";
        var newEmail = $"security-email-changed-{Guid.NewGuid():N}@test.local";
        var user = await ManagementSecurityTestSupport.CreateDualRoleUserAsync(factory, email);

        using var adminClient = factory.CreateClientWithoutRedirects();
        using var superAdminClient = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(adminClient, "/admin", email)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(superAdminClient, "/superadmin", email)).StatusCode);

        using var begin = await FormClient.PostFormAsync(
            adminClient,
            "/admin/security",
            "/admin/security/email-change",
            new Dictionary<string, string>
            {
                ["EmailChange.CurrentPassword"] = AeternumWebApplicationFactory.Password,
                ["EmailChange.NewEmail"] = newEmail,
            });
        Assert.Equal(HttpStatusCode.Redirect, begin.StatusCode);
        Assert.Contains("/admin/security", begin.Headers.Location?.OriginalString, StringComparison.Ordinal);

        using var flash = await adminClient.GetAsync(begin.Headers.Location);
        var flashHtml = WebUtility.HtmlDecode(await flash.Content.ReadAsStringAsync());
        Assert.Contains("Doğrulama bağlantısı gönderildi", flashHtml, StringComparison.Ordinal);
        Assert.Contains("data-server-flash-kind=\"success\"", flashHtml, StringComparison.Ordinal);

        var confirmationPath = await ManagementSecurityTestSupport.ReadConfirmationPathAsync(factory, user.Id, newEmail);
        Assert.StartsWith("/admin/security/email-change/confirm", confirmationPath, StringComparison.Ordinal);

        using var anonymousClient = factory.CreateClientWithoutRedirects();
        var (confirmationGet, antiforgeryToken) = await FormClient.GetFormAsync(anonymousClient, confirmationPath);
        var confirmationHtml = WebUtility.HtmlDecode(await confirmationGet.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, confirmationGet.StatusCode);
        Assert.Contains("no-store", confirmationGet.Headers.CacheControl?.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("no-referrer", Assert.Single(confirmationGet.Headers.GetValues("Referrer-Policy")));
        Assert.Contains("action=\"/admin/security/email-change/confirm\"", confirmationHtml, StringComparison.Ordinal);
        Assert.Contains("class=\"account-layout\"", confirmationHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("data-idle-session", confirmationHtml, StringComparison.Ordinal);

        var token = ManagementSecurityTestSupport.ExtractHiddenValue(confirmationHtml, "Token");
        await ManagementSecurityTestSupport.AssertEmailUnchangedAsync(factory, email, newEmail);

        using var tamperedClient = factory.CreateClientWithoutRedirects();
        var tamperedPath = confirmationPath.Replace(user.Id.ToString("D"), Guid.NewGuid().ToString("D"), StringComparison.OrdinalIgnoreCase);
        var tamperedHtml = await (await tamperedClient.GetAsync(tamperedPath)).Content.ReadAsStringAsync();
        Assert.Contains("data-management-email-change-state=\"invalid\"", tamperedHtml, StringComparison.Ordinal);

        using var confirm = await FormClient.PostWithTokenAsync(
            anonymousClient,
            "/admin/security/email-change/confirm",
            antiforgeryToken,
            new Dictionary<string, string>
            {
                ["UserId"] = user.Id.ToString("D"),
                ["NewEmail"] = newEmail,
                ["Token"] = token,
            });
        Assert.Equal(HttpStatusCode.Redirect, confirm.StatusCode);
        Assert.Equal("/admin/login?reason=credentials-changed", confirm.Headers.Location?.OriginalString);

        Assert.Equal(HttpStatusCode.Redirect, (await adminClient.GetAsync("/admin/security")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await superAdminClient.GetAsync("/superadmin/security")).StatusCode);

        using var newAdminClient = factory.CreateClientWithoutRedirects();
        using var newSuperAdminClient = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(newAdminClient, "/admin", newEmail)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(newSuperAdminClient, "/superadmin", newEmail)).StatusCode);

        using var oldEmailClient = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.OK, (await FormClient.LoginAsync(oldEmailClient, "/admin", email)).StatusCode);

        using var replay = await FormClient.PostWithTokenAsync(
            anonymousClient,
            "/admin/security/email-change/confirm",
            antiforgeryToken,
            new Dictionary<string, string>
            {
                ["UserId"] = user.Id.ToString("D"),
                ["NewEmail"] = newEmail,
                ["Token"] = token,
            });
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var changed = await db.Users.SingleAsync(candidate => candidate.Id == user.Id);
        Assert.Equal(newEmail, changed.Email);
        Assert.Equal(newEmail, changed.UserName);
        Assert.True(await db.NotificationDeliveries.AnyAsync(delivery => delivery.UserId == user.Id && delivery.Destination == email));
        var audit = await db.AuditLogs.SingleAsync(log => log.AdminUserId == user.Id && log.ActionType == "ManagementEmailChanged");
        Assert.DoesNotContain(email, audit.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(newEmail, audit.Description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(token, audit.Description, StringComparison.Ordinal);
    }
}

public sealed class ManagementSecurityPasswordChangeTests(AeternumWebApplicationFactory factory)
    : IClassFixture<AeternumWebApplicationFactory>
{
    [Fact]
    public async Task Superadmin_password_change_rejects_invalid_input_changes_login_and_revokes_both_portals()
    {
        var email = $"security-password-{Guid.NewGuid():N}@test.local";
        var newPassword = "AnotherValid2!";
        var user = await ManagementSecurityTestSupport.CreateDualRoleUserAsync(factory, email);

        using var adminClient = factory.CreateClientWithoutRedirects();
        using var superAdminClient = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(adminClient, "/admin", email)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(superAdminClient, "/superadmin", email)).StatusCode);

        using var invalid = await FormClient.PostFormAsync(
            superAdminClient,
            "/superadmin/security",
            "/superadmin/security/password",
            new Dictionary<string, string>
            {
                ["PasswordChange.CurrentPassword"] = "wrong-password",
                ["PasswordChange.NewPassword"] = newPassword,
                ["PasswordChange.ConfirmPassword"] = newPassword,
            });
        var invalidHtml = WebUtility.HtmlDecode(await invalid.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        Assert.Contains("management-security__form-error", invalidHtml, StringComparison.Ordinal);

        using var changed = await FormClient.PostFormAsync(
            superAdminClient,
            "/superadmin/security",
            "/superadmin/security/password",
            new Dictionary<string, string>
            {
                ["PasswordChange.CurrentPassword"] = AeternumWebApplicationFactory.Password,
                ["PasswordChange.NewPassword"] = newPassword,
                ["PasswordChange.ConfirmPassword"] = newPassword,
            });
        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        Assert.Equal("/superadmin/login?reason=credentials-changed", changed.Headers.Location?.OriginalString);

        Assert.Equal(HttpStatusCode.Redirect, (await adminClient.GetAsync("/admin/security")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await superAdminClient.GetAsync("/superadmin/security")).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(0, await db.ManagementSessions.CountAsync(session => session.UserId == user.Id && !session.RevokedAtUtc.HasValue));
            var audit = await db.AuditLogs.SingleAsync(log => log.AdminUserId == user.Id && log.ActionType == "ManagementPasswordChanged");
            Assert.DoesNotContain(AeternumWebApplicationFactory.Password, audit.Description, StringComparison.Ordinal);
            Assert.DoesNotContain(newPassword, audit.Description, StringComparison.Ordinal);
        }

        using var oldPasswordClient = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.OK, (await FormClient.LoginAsync(oldPasswordClient, "/admin", email)).StatusCode);
        using var newPasswordAdminClient = factory.CreateClientWithoutRedirects();
        using var newPasswordSuperAdminClient = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(newPasswordAdminClient, "/admin", email, password: newPassword)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(newPasswordSuperAdminClient, "/superadmin", email, password: newPassword)).StatusCode);
    }
}

internal static class ManagementSecurityTestSupport
{
    public static async Task<ApplicationUser> CreateDualRoleUserAsync(
        AeternumWebApplicationFactory factory,
        string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = "Security",
            LastName = "Manager",
            CreatedAtUtc = factory.Clock.GetUtcNow(),
            IsActive = true,
        };
        Assert.True((await userManager.CreateAsync(user, AeternumWebApplicationFactory.Password)).Succeeded);
        Assert.True((await userManager.AddToRolesAsync(user, [RoleNames.Admin, RoleNames.SuperAdmin])).Succeeded);
        return user;
    }

    public static async Task<string> ReadConfirmationPathAsync(
        AeternumWebApplicationFactory factory,
        Guid userId,
        string destination)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var protectedPayload = await db.NotificationDeliveries
            .Where(item => item.UserId == userId
                && item.Destination == destination
                && item.TemplateKey == OutboxIdentityMessageSender.ProtectedTemplateKey)
            .Select(item => item.PayloadJson)
            .SingleAsync();
        var protector = factory.Services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(OutboxIdentityMessageSender.DataProtectionPurpose);
        var payload = Assert.IsType<DeliveryPayload>(
            JsonSerializer.Deserialize<DeliveryPayload>(protector.Unprotect(protectedPayload)));
        var match = Regex.Match(payload.Body, "href=\\\"([^\\\"]+)\\\"");
        Assert.True(match.Success);
        var url = new Uri(WebUtility.HtmlDecode(match.Groups[1].Value), UriKind.Absolute);
        return url.PathAndQuery;
    }

    public static async Task AssertEmailUnchangedAsync(
        AeternumWebApplicationFactory factory,
        string email,
        string newEmail)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.NotNull(await manager.FindByEmailAsync(email));
        Assert.Null(await manager.FindByEmailAsync(newEmail));
    }

    public static string ExtractHiddenValue(string html, string name)
    {
        var input = Regex.Match(html, $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*>", RegexOptions.IgnoreCase);
        Assert.True(input.Success, $"Hidden input '{name}' was not rendered.");
        var value = Regex.Match(input.Value, "value=\"([^\"]*)\"", RegexOptions.IgnoreCase);
        Assert.True(value.Success, $"Hidden input '{name}' has no value.");
        return WebUtility.HtmlDecode(value.Groups[1].Value);
    }

    public static int CountOccurrences(string value, string fragment) =>
        value.Split(fragment, StringSplitOptions.None).Length - 1;

}
