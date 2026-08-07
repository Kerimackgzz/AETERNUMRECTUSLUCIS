using System.Net;
using AETKAHVE.Application.Commerce;
using AETKAHVE.Application.Security;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Infrastructure.Identity;
using AETKAHVE.Infrastructure.Persistence;
using AETKAHVE.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AETKAHVE.IntegrationTests;

public sealed class CustomerAccountCenterTests(AeternumWebApplicationFactory factory)
    : IClassFixture<AeternumWebApplicationFactory>
{
    [Fact]
    public async Task Account_dashboard_uses_real_customer_data_and_accessible_editorial_navigation()
    {
        using var client = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(
            client,
            "/account",
            AeternumWebApplicationFactory.CustomerEmail)).StatusCode);

        var response = await client.GetAsync("/account");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Hoş geldiniz, Test", html, StringComparison.Ordinal);
        Assert.Contains("data-account-menu-toggle", html, StringComparison.Ordinal);
        Assert.Contains("aria-controls=\"customer-account-menu\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/account\"", html, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"page\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/cart\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/favorites\"", html, StringComparison.Ordinal);
        Assert.Contains("id=\"profile\"", html, StringComparison.Ordinal);
        Assert.Contains("Sepetim", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Hesap panelinize hoş geldiniz.", html, StringComparison.Ordinal);

        var script = await (await client.GetAsync("/js/pages/customer-account.js")).Content.ReadAsStringAsync();
        var css = await (await client.GetAsync("/css/pages/customer-account.css")).Content.ReadAsStringAsync();
        Assert.Contains("event.key === \"Escape\"", script, StringComparison.Ordinal);
        Assert.Contains("mainContent.inert = open", script, StringComparison.Ordinal);
        Assert.Contains("siteNavbar.inert = open", script, StringComparison.Ordinal);
        Assert.Contains("account-menu-open", script, StringComparison.Ordinal);
        Assert.Contains("lastFocused.focus()", script, StringComparison.Ordinal);
        Assert.Contains("@media (prefers-reduced-motion: reduce)", css, StringComparison.Ordinal);
        Assert.DoesNotContain("backdrop-filter", css, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Profile_update_is_owner_scoped_does_not_change_email_and_requires_antiforgery()
    {
        var email = $"profile-{Guid.NewGuid():N}@test.local";
        await CreateCustomerAsync(email);
        using var client = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(client, "/account", email)).StatusCode);

        using var missingToken = await client.PostAsync(
            "/account/profile",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Profile.FirstName"] = "Ada",
                ["Profile.LastName"] = "Lovelace",
            }));
        Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);

        using var response = await FormClient.PostFormAsync(
            client,
            "/account",
            "/account/profile",
            new Dictionary<string, string>
            {
                ["Profile.FirstName"] = "Ada",
                ["Profile.LastName"] = "Lovelace",
                ["Profile.PhoneNumber"] = "+905551112233",
                ["Profile.DateOfBirth"] = "1990-01-02",
            });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var updated = Assert.IsType<ApplicationUser>(await userManager.FindByEmailAsync(email));
        Assert.Equal("Ada", updated.FirstName);
        Assert.Equal("Lovelace", updated.LastName);
        Assert.Equal(email, updated.Email);
        Assert.Equal(new DateOnly(1990, 1, 2), updated.DateOfBirth);
    }

    [Fact]
    public async Task Profile_photo_enforces_two_mib_and_content_signature_then_cleans_up_replacements()
    {
        var email = $"photo-{Guid.NewGuid():N}@test.local";
        var user = await CreateCustomerAsync(email);
        await using var scope = factory.Services.CreateAsyncScope();
        var profiles = scope.ServiceProvider.GetRequiredService<ICustomerProfileService>();

        var oversized = await profiles.SavePhotoAsync(
            user.Id,
            Stream.Null,
            2 * 1024 * 1024 + 1,
            "avatar.png",
            "image/png",
            default);
        Assert.False(oversized.Succeeded);

        await using var invalidContent = new MemoryStream("not-an-image"u8.ToArray());
        var invalid = await profiles.SavePhotoAsync(
            user.Id,
            invalidContent,
            invalidContent.Length,
            "avatar.png",
            "image/png",
            default);
        Assert.False(invalid.Succeeded);

        var firstBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        await using var firstContent = new MemoryStream(firstBytes);
        Assert.True((await profiles.SavePhotoAsync(user.Id, firstContent, firstContent.Length, "first.png", "image/png", default)).Succeeded);
        var firstPhoto = Assert.IsType<CustomerProfilePhoto>(await profiles.OpenPhotoAsync(user.Id, default));
        await firstPhoto.Content.DisposeAsync();

        var secondBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 };
        await using var secondContent = new MemoryStream(secondBytes);
        Assert.True((await profiles.SavePhotoAsync(user.Id, secondContent, secondContent.Length, "second.jpg", "image/jpeg", default)).Succeeded);
        var secondPhoto = Assert.IsType<CustomerProfilePhoto>(await profiles.OpenPhotoAsync(user.Id, default));
        Assert.Equal("image/jpeg", secondPhoto.ContentType);
        await secondPhoto.Content.DisposeAsync();

        Assert.True((await profiles.DeletePhotoAsync(user.Id, default)).Succeeded);
        Assert.Null(await profiles.OpenPhotoAsync(user.Id, default));
    }

    [Fact]
    public async Task Email_change_rejects_wrong_password_duplicate_and_replay_then_invalidates_session()
    {
        var email = $"email-{Guid.NewGuid():N}@test.local";
        var newEmail = $"changed-{Guid.NewGuid():N}@test.local";
        var user = await CreateCustomerAsync(email);
        using var client = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(client, "/account", email)).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var profiles = scope.ServiceProvider.GetRequiredService<ICustomerProfileService>();
            Assert.False((await profiles.BeginEmailChangeAsync(user.Id, "wrong", newEmail, default)).Succeeded);
            Assert.False((await profiles.BeginEmailChangeAsync(
                user.Id,
                AeternumWebApplicationFactory.Password,
                AeternumWebApplicationFactory.CustomerEmail,
                default)).Succeeded);

            var start = await profiles.BeginEmailChangeAsync(
                user.Id,
                AeternumWebApplicationFactory.Password,
                newEmail,
                default);
            Assert.True(start.Succeeded);
            Assert.NotNull(start.Token);
            Assert.True((await profiles.ValidateEmailChangeAsync(user.Id, newEmail, start.Token!, default)).CanConfirm);
            Assert.True((await profiles.ConfirmEmailChangeAsync(user.Id, newEmail, start.Token!, default)).Succeeded);
            Assert.False((await profiles.ConfirmEmailChangeAsync(user.Id, newEmail, start.Token!, default)).Succeeded);
        }

        var staleSession = await client.GetAsync("/account");
        Assert.Equal(HttpStatusCode.Redirect, staleSession.StatusCode);
        Assert.Contains("/account/login", staleSession.Headers.Location?.OriginalString, StringComparison.Ordinal);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var db = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var changed = await db.Users.SingleAsync(candidate => candidate.Id == user.Id);
        Assert.Equal(newEmail, changed.Email);
        Assert.Equal(newEmail, changed.UserName);
        Assert.True(await db.NotificationDeliveries.AnyAsync(delivery => delivery.UserId == user.Id && delivery.Destination == email));
    }

    [Fact]
    public async Task Password_change_applies_identity_policy_rejects_old_password_and_invalidates_session()
    {
        var email = $"password-{Guid.NewGuid():N}@test.local";
        var newPassword = "AnotherValid2!";
        var user = await CreateCustomerAsync(email);
        using var client = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(client, "/account", email)).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var profiles = scope.ServiceProvider.GetRequiredService<ICustomerProfileService>();
            Assert.False((await profiles.ChangePasswordAsync(user.Id, "wrong", newPassword, default)).Succeeded);
            Assert.False((await profiles.ChangePasswordAsync(user.Id, AeternumWebApplicationFactory.Password, "short", default)).Succeeded);
            Assert.True((await profiles.ChangePasswordAsync(user.Id, AeternumWebApplicationFactory.Password, newPassword, default)).Succeeded);
        }

        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/account")).StatusCode);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var manager = verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var changed = Assert.IsType<ApplicationUser>(await manager.FindByEmailAsync(email));
        Assert.False(await manager.CheckPasswordAsync(changed, AeternumWebApplicationFactory.Password));
        Assert.True(await manager.CheckPasswordAsync(changed, newPassword));
    }

    private async Task<ApplicationUser> CreateCustomerAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = "Account",
            LastName = "Customer",
            CreatedAtUtc = factory.Clock.GetUtcNow(),
            IsActive = true,
        };
        Assert.True((await userManager.CreateAsync(user, AeternumWebApplicationFactory.Password)).Succeeded);
        Assert.True((await userManager.AddToRoleAsync(user, RoleNames.Customer)).Succeeded);
        return user;
    }
}
