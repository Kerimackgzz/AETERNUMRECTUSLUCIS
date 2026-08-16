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
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace AETKAHVE.IntegrationTests;

public sealed class AdminAccountManagementTests(AeternumWebApplicationFactory factory)
    : IClassFixture<AeternumWebApplicationFactory>
{
    [Fact]
    public async Task Management_surface_is_superadmin_only_and_never_lists_superadmin()
    {
        using var anonymous = factory.CreateClientWithoutRedirects();
        var anonymousResponse = await anonymous.GetAsync("/superadmin/admins");
        Assert.Equal(HttpStatusCode.Redirect, anonymousResponse.StatusCode);
        Assert.Equal("/superadmin/login", anonymousResponse.Headers.Location?.AbsolutePath);

        using var admin = factory.CreateClientWithoutRedirects();
        Assert.Equal(
            HttpStatusCode.Redirect,
            (await FormClient.LoginAsync(admin, "/admin", AeternumWebApplicationFactory.AdminEmail)).StatusCode);
        var forbidden = await admin.GetAsync("/superadmin/admins");
        Assert.Equal(HttpStatusCode.Redirect, forbidden.StatusCode);
        Assert.Equal("/superadmin/login", forbidden.Headers.Location?.AbsolutePath);

        var adminHome = WebUtility.HtmlDecode(await (await admin.GetAsync("/admin")).Content.ReadAsStringAsync());
        Assert.DoesNotContain("href=\"/superadmin/admins\"", adminHome, StringComparison.Ordinal);

        using var superAdmin = await LoginSuperAdminAsync();
        var response = await superAdmin.GetAsync("/superadmin/admins");
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Admin Yönetimi", html, StringComparison.Ordinal);
        Assert.Contains(AeternumWebApplicationFactory.AdminEmail, html, StringComparison.Ordinal);
        Assert.DoesNotContain(AeternumWebApplicationFactory.SuperAdminEmail, html, StringComparison.Ordinal);
        Assert.Contains("href=\"/superadmin/admins\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invitation_creates_admin_only_account_and_activation_controls_login_and_sessions()
    {
        var email = $"invited-admin-{Guid.NewGuid():N}@test.local";
        const string invitedPassword = "InvitedAdmin2!";
        using var superAdmin = await LoginSuperAdminAsync();

        using var created = await FormClient.PostFormAsync(
            superAdmin,
            "/superadmin/admins/create",
            "/superadmin/admins/create",
            new Dictionary<string, string>
            {
                ["FirstName"] = "Davetli",
                ["LastName"] = "Admin",
                ["Email"] = email,
                ["Role"] = RoleNames.SuperAdmin,
            });
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        Assert.Equal("/superadmin/admins", created.Headers.Location?.OriginalString);

        ApplicationUser user;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            user = Assert.IsType<ApplicationUser>(await userManager.FindByEmailAsync(email));
            Assert.False(user.IsActive);
            Assert.False(user.EmailConfirmed);
            Assert.Null(user.PasswordHash);
            Assert.True(await userManager.IsInRoleAsync(user, RoleNames.Admin));
            Assert.False(await userManager.IsInRoleAsync(user, RoleNames.SuperAdmin));
        }

        var invitationPath = await ReadIdentityPathAsync(user.Id, email, "/admin/invitation");
        Assert.StartsWith("/admin/invitation", invitationPath, StringComparison.Ordinal);
        using var invited = factory.CreateClientWithoutRedirects();
        var (invitationGet, antiforgery) = await FormClient.GetFormAsync(invited, invitationPath);
        var invitationHtml = WebUtility.HtmlDecode(await invitationGet.Content.ReadAsStringAsync());
        Assert.Contains("Admin davetini tamamla", invitationHtml, StringComparison.Ordinal);
        Assert.Equal("no-referrer", Assert.Single(invitationGet.Headers.GetValues("Referrer-Policy")));

        using var accepted = await FormClient.PostWithTokenAsync(
            invited,
            "/admin/invitation",
            antiforgery,
            new Dictionary<string, string>
            {
                ["UserId"] = user.Id.ToString("D"),
                ["Token"] = ManagementSecurityTestSupport.ExtractHiddenValue(invitationHtml, "Token"),
                ["Password"] = invitedPassword,
                ["ConfirmPassword"] = invitedPassword,
            });
        Assert.Equal(HttpStatusCode.Redirect, accepted.StatusCode);
        Assert.Equal("/admin/login", accepted.Headers.Location?.OriginalString);

        using var adminSession = factory.CreateClientWithoutRedirects();
        Assert.Equal(
            HttpStatusCode.Redirect,
            (await FormClient.LoginAsync(adminSession, "/admin", email, password: invitedPassword)).StatusCode);
        using var cannotUseSuperAdmin = factory.CreateClientWithoutRedirects();
        Assert.Equal(
            HttpStatusCode.OK,
            (await FormClient.LoginAsync(cannotUseSuperAdmin, "/superadmin", email, password: invitedPassword)).StatusCode);

        using var deactivated = await FormClient.PostFormAsync(
            superAdmin,
            "/superadmin/admins",
            $"/superadmin/admins/{user.Id:D}/deactivate",
            new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.Redirect, deactivated.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await adminSession.GetAsync("/admin")).StatusCode);
        using var inactiveLogin = factory.CreateClientWithoutRedirects();
        Assert.Equal(
            HttpStatusCode.OK,
            (await FormClient.LoginAsync(inactiveLogin, "/admin", email, password: invitedPassword)).StatusCode);

        using var activated = await FormClient.PostFormAsync(
            superAdmin,
            "/superadmin/admins",
            $"/superadmin/admins/{user.Id:D}/activate",
            new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.Redirect, activated.StatusCode);
        using var activeLogin = factory.CreateClientWithoutRedirects();
        Assert.Equal(
            HttpStatusCode.Redirect,
            (await FormClient.LoginAsync(activeLogin, "/admin", email, password: invitedPassword)).StatusCode);

        await using var auditScope = factory.Services.CreateAsyncScope();
        var auditDb = auditScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await auditDb.AuditLogs.AnyAsync(log =>
            log.EntityId == user.Id.ToString("D") && log.ActionType == "AdminAccountCreated"));
        Assert.True(await auditDb.AuditLogs.AnyAsync(log =>
            log.EntityId == user.Id.ToString("D") && log.ActionType == "AdminInvitationCompleted"));
        Assert.True(await auditDb.AuditLogs.AnyAsync(log =>
            log.EntityId == user.Id.ToString("D") && log.ActionType == "AdminAccountDeactivated"));
    }

    [Fact]
    public async Task Renewed_invitation_invalidates_old_link_and_password_reset_keeps_passive_state()
    {
        var email = $"renewed-admin-{Guid.NewGuid():N}@test.local";
        const string firstPassword = "FirstAdmin3!";
        const string secondPassword = "SecondAdmin4!";
        using var superAdmin = await LoginSuperAdminAsync();
        var user = await CreatePendingAdminThroughUiAsync(superAdmin, email);
        var oldInvitation = await ReadIdentityPathAsync(user.Id, email, "/admin/invitation");

        using var renewed = await FormClient.PostFormAsync(
            superAdmin,
            "/superadmin/admins",
            $"/superadmin/admins/{user.Id:D}/resend-invitation",
            new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.Redirect, renewed.StatusCode);
        var newInvitation = await ReadIdentityPathAsync(user.Id, email, "/admin/invitation", oldInvitation);
        Assert.NotEqual(oldInvitation, newInvitation);

        using var anonymous = factory.CreateClientWithoutRedirects();
        var oldHtml = WebUtility.HtmlDecode(await (await anonymous.GetAsync(oldInvitation)).Content.ReadAsStringAsync());
        Assert.Contains("geçersiz, kullanılmış veya süresi dolmuş", oldHtml, StringComparison.Ordinal);

        await CompletePasswordTokenAsync(anonymous, newInvitation, "/admin/invitation", user.Id, firstPassword);

        using var madePassive = await FormClient.PostFormAsync(
            superAdmin,
            "/superadmin/admins",
            $"/superadmin/admins/{user.Id:D}/deactivate",
            new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.Redirect, madePassive.StatusCode);

        using var resetRequested = await FormClient.PostFormAsync(
            superAdmin,
            "/superadmin/admins",
            $"/superadmin/admins/{user.Id:D}/password-reset",
            new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.Redirect, resetRequested.StatusCode);
        var resetPath = await ReadIdentityPathAsync(user.Id, email, "/admin/password-reset");
        Assert.StartsWith("/admin/password-reset", resetPath, StringComparison.Ordinal);
        await CompletePasswordTokenAsync(anonymous, resetPath, "/admin/password-reset", user.Id, secondPassword);

        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var passive = Assert.IsType<ApplicationUser>(await userManager.FindByIdAsync(user.Id.ToString()));
        Assert.False(passive.IsActive);
        Assert.True(await userManager.CheckPasswordAsync(passive, secondPassword));
        Assert.False(await userManager.CheckPasswordAsync(passive, firstPassword));
    }

    [Fact]
    public async Task Email_change_waits_for_confirmation_and_permanent_delete_removes_identity()
    {
        var email = $"managed-admin-{Guid.NewGuid():N}@test.local";
        var newEmail = $"managed-admin-new-{Guid.NewGuid():N}@test.local";
        var user = await CreateActiveAdminAsync(email);
        using var superAdmin = await LoginSuperAdminAsync();
        using var adminSession = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(adminSession, "/admin", email)).StatusCode);

        using var edit = await FormClient.PostFormAsync(
            superAdmin,
            $"/superadmin/admins/{user.Id:D}/edit",
            $"/superadmin/admins/{user.Id:D}/edit",
            new Dictionary<string, string>
            {
                ["Id"] = user.Id.ToString("D"),
                ["FirstName"] = "Güncel",
                ["LastName"] = "Admin",
                ["Email"] = newEmail,
            });
        Assert.Equal(HttpStatusCode.Redirect, edit.StatusCode);

        await using (var unchangedScope = factory.Services.CreateAsyncScope())
        {
            var manager = unchangedScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.NotNull(await manager.FindByEmailAsync(email));
            Assert.Null(await manager.FindByEmailAsync(newEmail));
        }

        var confirmationPath = await ReadIdentityPathAsync(user.Id, newEmail, "/admin/email-change/confirm");
        Assert.StartsWith("/admin/email-change/confirm", confirmationPath, StringComparison.Ordinal);
        using var anonymous = factory.CreateClientWithoutRedirects();
        var (confirmGet, token) = await FormClient.GetFormAsync(anonymous, confirmationPath);
        var confirmHtml = WebUtility.HtmlDecode(await confirmGet.Content.ReadAsStringAsync());
        using var confirm = await FormClient.PostWithTokenAsync(
            anonymous,
            "/admin/email-change/confirm",
            token,
            new Dictionary<string, string>
            {
                ["UserId"] = user.Id.ToString("D"),
                ["NewEmail"] = newEmail,
                ["Token"] = ManagementSecurityTestSupport.ExtractHiddenValue(confirmHtml, "Token"),
            });
        Assert.Equal(HttpStatusCode.Redirect, confirm.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await adminSession.GetAsync("/admin")).StatusCode);

        using var deleteGet = await superAdmin.GetAsync($"/superadmin/admins/{user.Id:D}/delete");
        var deleteHtml = WebUtility.HtmlDecode(await deleteGet.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, deleteGet.StatusCode);
        Assert.Contains("geri alınamaz", deleteHtml, StringComparison.OrdinalIgnoreCase);

        using var missingAntiforgery = await superAdmin.PostAsync(
            $"/superadmin/admins/{user.Id:D}/delete",
            new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.BadRequest, missingAntiforgery.StatusCode);

        using var deleted = await FormClient.PostFormAsync(
            superAdmin,
            $"/superadmin/admins/{user.Id:D}/delete",
            $"/superadmin/admins/{user.Id:D}/delete",
            new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);

        await using var deletedScope = factory.Services.CreateAsyncScope();
        var db = deletedScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Users.AnyAsync(candidate => candidate.Id == user.Id));
        Assert.False(await db.ManagementSessions.AnyAsync(session => session.UserId == user.Id));
        Assert.True(await db.AuditLogs.AnyAsync(log =>
            log.EntityId == user.Id.ToString("D") && log.ActionType == "AdminAccountPermanentlyDeleted"));
    }

    private async Task<HttpClient> LoginSuperAdminAsync()
    {
        var client = factory.CreateClientWithoutRedirects();
        var login = await FormClient.LoginAsync(
            client,
            "/superadmin",
            AeternumWebApplicationFactory.SuperAdminEmail);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        return client;
    }

    private async Task<ApplicationUser> CreatePendingAdminThroughUiAsync(HttpClient superAdmin, string email)
    {
        var response = await FormClient.PostFormAsync(
            superAdmin,
            "/superadmin/admins/create",
            "/superadmin/admins/create",
            new Dictionary<string, string>
            {
                ["FirstName"] = "Pending",
                ["LastName"] = "Admin",
                ["Email"] = email,
            });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        return Assert.IsType<ApplicationUser>(await manager.FindByEmailAsync(email));
    }

    private async Task<ApplicationUser> CreateActiveAdminAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = "Managed",
            LastName = "Admin",
            CreatedAtUtc = factory.Clock.GetUtcNow(),
            IsActive = true,
            LockoutEnabled = true,
        };
        Assert.True((await manager.CreateAsync(user, AeternumWebApplicationFactory.Password)).Succeeded);
        Assert.True((await manager.AddToRoleAsync(user, RoleNames.Admin)).Succeeded);
        return user;
    }

    private async Task<string> ReadIdentityPathAsync(
        Guid userId,
        string destination,
        string pathPrefix,
        string? excludedPath = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var deliveries = await db.NotificationDeliveries
            .Where(item => item.UserId == userId
                && item.Destination == destination
                && item.TemplateKey == OutboxIdentityMessageSender.ProtectedTemplateKey)
            .Select(item => new { item.CreatedAtUtc, item.PayloadJson })
            .ToListAsync();
        var protector = factory.Services.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(OutboxIdentityMessageSender.DataProtectionPurpose);
        var paths = deliveries.Select(delivery =>
        {
            var payload = Assert.IsType<DeliveryPayload>(
                JsonSerializer.Deserialize<DeliveryPayload>(protector.Unprotect(delivery.PayloadJson)));
            var match = Regex.Match(payload.Body, "href=\\\"([^\\\"]+)\\\"");
            Assert.True(match.Success);
            return new Uri(WebUtility.HtmlDecode(match.Groups[1].Value), UriKind.Absolute).PathAndQuery;
        }).Where(path => path.StartsWith(pathPrefix, StringComparison.Ordinal)
            && !string.Equals(path, excludedPath, StringComparison.Ordinal)).ToArray();
        return Assert.Single(paths);
    }

    private static async Task CompletePasswordTokenAsync(
        HttpClient client,
        string getPath,
        string postPath,
        Guid userId,
        string password)
    {
        var (response, antiforgery) = await FormClient.GetFormAsync(client, getPath);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        using var completed = await FormClient.PostWithTokenAsync(
            client,
            postPath,
            antiforgery,
            new Dictionary<string, string>
            {
                ["UserId"] = userId.ToString("D"),
                ["Token"] = ManagementSecurityTestSupport.ExtractHiddenValue(html, "Token"),
                ["Password"] = password,
                ["ConfirmPassword"] = password,
            });
        Assert.Equal(HttpStatusCode.Redirect, completed.StatusCode);
    }
}

public sealed class SingleSuperAdminInvariantTests(AeternumWebApplicationFactory factory)
    : IClassFixture<AeternumWebApplicationFactory>
{
    [Fact]
    public async Task Startup_invariant_rejects_more_than_one_superadmin()
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var email = $"second-superadmin-{Guid.NewGuid():N}@test.local";
            var second = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = "Second",
                LastName = "SuperAdmin",
                CreatedAtUtc = factory.Clock.GetUtcNow(),
                IsActive = true,
            };
            Assert.True((await manager.CreateAsync(second, AeternumWebApplicationFactory.Password)).Succeeded);
            Assert.True((await manager.AddToRoleAsync(second, RoleNames.SuperAdmin)).Succeeded);
        }

        var guard = new SingleSuperAdminInvariantHostedService(
            factory.Services.GetRequiredService<IServiceScopeFactory>(),
            new TestEnvironment(Environments.Development));
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => guard.StartAsync(default));
        Assert.Contains("more than one SuperAdmin", exception.Message, StringComparison.Ordinal);
    }

    private sealed class TestEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "AETKAHVE.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
