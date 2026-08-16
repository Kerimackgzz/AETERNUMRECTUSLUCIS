using System.Net;
using System.Text.RegularExpressions;
using AETKAHVE.Application.Security;
using AETKAHVE.Infrastructure.Identity;
using AETKAHVE.Infrastructure.Notifications;
using AETKAHVE.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AETKAHVE.IntegrationTests;

public sealed class CookieAndIdentityTests(AeternumWebApplicationFactory factory)
    : IClassFixture<AeternumWebApplicationFactory>
{
    [Fact]
    public async Task Registration_requires_email_confirmation_then_allows_login()
    {
        var email = $"new-{Guid.NewGuid():N}@test.local";
        using var client = factory.CreateClientWithoutRedirects();
        var registration = await FormClient.PostFormAsync(
            client,
            "/account/register",
            "/account/register",
            new Dictionary<string, string>
            {
                ["FirstName"] = "Yeni",
                ["LastName"] = "Müşteri",
                ["Email"] = email,
                ["Password"] = AeternumWebApplicationFactory.Password,
                ["ConfirmPassword"] = AeternumWebApplicationFactory.Password,
                ["AcceptPrivacyTerms"] = "true",
            });
        Assert.Equal(HttpStatusCode.Redirect, registration.StatusCode);

        var message = factory.Services.GetRequiredService<InMemoryIdentityMessageSender>().Messages
            .Last(item => string.Equals(item.Destination, email, StringComparison.OrdinalIgnoreCase));
        var linkMatch = Regex.Match(message.HtmlBody, "href=\\\"([^\\\"]+)\\\"");
        Assert.True(linkMatch.Success);
        var confirmationUrl = WebUtility.HtmlDecode(linkMatch.Groups[1].Value);

        await AssertCustomerDoesNotExistAsync(email);

        var confirmationPage = await client.GetAsync(confirmationUrl);
        var confirmationHtml = await confirmationPage.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, confirmationPage.StatusCode);
        Assert.Contains("data-confirm-email-state=\"ready\"", confirmationHtml, StringComparison.Ordinal);
        Assert.Contains("data-confirm-email-form", confirmationHtml, StringComparison.Ordinal);
        Assert.Contains("Üyeliği tamamla", confirmationHtml, StringComparison.Ordinal);
        await AssertCustomerDoesNotExistAsync(email);

        var completion = await FormClient.PostWithTokenAsync(
            client,
            "/account/confirm-email",
            ExtractHiddenValue(confirmationHtml, "__RequestVerificationToken"),
            new Dictionary<string, string>
            {
                ["RegistrationId"] = ExtractHiddenValue(confirmationHtml, "RegistrationId"),
                ["Token"] = ExtractHiddenValue(confirmationHtml, "Token"),
            });

        Assert.Equal(HttpStatusCode.Redirect, completion.StatusCode);
        Assert.Equal("/account/login", completion.Headers.Location?.ToString());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = Assert.IsType<ApplicationUser>(await userManager.FindByEmailAsync(email));
            Assert.True(user.EmailConfirmed);
        }

        using var loginClient = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(
            loginClient,
            "/account",
            email)).StatusCode);
    }

    [Fact]
    public async Task Registering_an_existing_identity_returns_an_explicit_message_without_sending_email()
    {
        var sender = factory.Services.GetRequiredService<InMemoryIdentityMessageSender>();
        var messageCount = sender.Messages.Count(message =>
            string.Equals(message.Destination, AeternumWebApplicationFactory.AdminEmail, StringComparison.OrdinalIgnoreCase));
        string? originalPasswordHash;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            originalPasswordHash = (await userManager.FindByEmailAsync(AeternumWebApplicationFactory.AdminEmail))?.PasswordHash;
        }

        using var client = factory.CreateClientWithoutRedirects();
        var response = await FormClient.PostFormAsync(
            client,
            "/account/register",
            "/account/register",
            new Dictionary<string, string>
            {
                ["FirstName"] = "Tekrar",
                ["LastName"] = "Kayıt",
                ["Email"] = AeternumWebApplicationFactory.AdminEmail,
                ["Password"] = AeternumWebApplicationFactory.Password,
                ["ConfirmPassword"] = AeternumWebApplicationFactory.Password,
                ["AcceptPrivacyTerms"] = "true",
            });
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Bu e-posta adresiyle kayıtlı bir hesap zaten var.", html, StringComparison.Ordinal);
        Assert.Contains("/account/login", html, StringComparison.Ordinal);
        Assert.Contains("/account/forgot-password", html, StringComparison.Ordinal);
        Assert.DoesNotContain("/admin/login", html, StringComparison.Ordinal);
        Assert.DoesNotContain("/superadmin/login", html, StringComparison.Ordinal);
        Assert.Equal(messageCount, sender.Messages.Count(message =>
            string.Equals(message.Destination, AeternumWebApplicationFactory.AdminEmail, StringComparison.OrdinalIgnoreCase)));

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationManager = verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var unchanged = Assert.IsType<ApplicationUser>(
            await verificationManager.FindByEmailAsync(AeternumWebApplicationFactory.AdminEmail));
        Assert.Equal(originalPasswordHash, unchanged.PasswordHash);
        Assert.True(await verificationManager.IsInRoleAsync(unchanged, RoleNames.Admin));
        Assert.False(await verificationManager.IsInRoleAsync(unchanged, RoleNames.Customer));
    }

    [Fact]
    public async Task Customer_login_hides_management_portals_and_wrong_portal_does_not_lock_management_identity()
    {
        var email = $"dual-{Guid.NewGuid():N}@test.local";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = "Dual",
                LastName = "Manager",
                CreatedAtUtc = factory.Clock.GetUtcNow(),
                IsActive = true,
            };
            Assert.True((await userManager.CreateAsync(user, AeternumWebApplicationFactory.Password)).Succeeded);
            Assert.True((await userManager.AddToRolesAsync(user, [RoleNames.Admin, RoleNames.SuperAdmin])).Succeeded);
        }

        using var customerClient = factory.CreateClientWithoutRedirects();
        for (var attempt = 0; attempt < 4; attempt++)
        {
            var rejected = await FormClient.LoginAsync(customerClient, "/account", email);
            var html = WebUtility.HtmlDecode(await rejected.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
            Assert.Contains("Giriş bilgileri geçersiz veya hesap kullanılamıyor", html, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/admin/login", html, StringComparison.Ordinal);
            Assert.DoesNotContain("/superadmin/login", html, StringComparison.Ordinal);
        }

        using var adminClient = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(adminClient, "/admin", email)).StatusCode);
        using var superAdminClient = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(superAdminClient, "/superadmin", email)).StatusCode);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationManager = verificationScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var unchanged = Assert.IsType<ApplicationUser>(await verificationManager.FindByEmailAsync(email));
        Assert.Equal(0, unchanged.AccessFailedCount);
        Assert.False(await verificationManager.IsLockedOutAsync(unchanged));
        Assert.False(await verificationManager.IsInRoleAsync(unchanged, RoleNames.Customer));
    }

    [Fact]
    public async Task Invalid_confirmation_link_renders_recovery_without_completion_form()
    {
        using var client = factory.CreateClientWithoutRedirects();

        var response = await client.GetAsync(
            $"/account/confirm-email?registrationId={Guid.NewGuid():D}&token=invalid");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("data-confirm-email-state=\"invalid\"", html, StringComparison.Ordinal);
        Assert.Contains("Bağlantı geçersiz veya süresi dolmuş", html, StringComparison.Ordinal);
        Assert.Contains("/account/resend-confirmation", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-confirm-email-form", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Confirmation_completion_requires_antiforgery_token()
    {
        using var client = factory.CreateClientWithoutRedirects();

        var response = await client.PostAsync(
            "/account/confirm-email",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["RegistrationId"] = Guid.NewGuid().ToString("D"),
                ["Token"] = "invalid",
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Remember_me_controls_cookie_persistence()
    {
        using var sessionClient = factory.CreateClientWithoutRedirects();
        var sessionResponse = await FormClient.LoginAsync(
            sessionClient,
            "/account",
            AeternumWebApplicationFactory.CustomerEmail,
            rememberMe: false);
        var sessionCookie = FindAuthenticationCookie(sessionResponse, "AETKAHVE.Customer.Auth");

        using var persistentClient = factory.CreateClientWithoutRedirects();
        var persistentResponse = await FormClient.LoginAsync(
            persistentClient,
            "/account",
            AeternumWebApplicationFactory.CustomerEmail,
            rememberMe: true);
        var persistentCookie = FindAuthenticationCookie(persistentResponse, "AETKAHVE.Customer.Auth");

        Assert.DoesNotContain("expires=", sessionCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", persistentCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", persistentCookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Password_reset_changes_the_customer_password()
    {
        const string newPassword = "ChangedPassword2!";
        string token;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(AeternumWebApplicationFactory.ResetEmail);
            token = await userManager.GeneratePasswordResetTokenAsync(Assert.IsType<ApplicationUser>(user));
        }

        using var client = factory.CreateClientWithoutRedirects();
        var encodedEmail = Uri.EscapeDataString(AeternumWebApplicationFactory.ResetEmail);
        var encodedToken = Uri.EscapeDataString(token);
        var response = await FormClient.PostFormAsync(
            client,
            $"/account/reset-password?email={encodedEmail}&token={encodedToken}",
            "/account/reset-password",
            new Dictionary<string, string>
            {
                ["Email"] = AeternumWebApplicationFactory.ResetEmail,
                ["Token"] = token,
                ["Password"] = newPassword,
                ["ConfirmPassword"] = newPassword,
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using (var loginPage = await client.GetAsync(response.Headers.Location))
        {
            var html = WebUtility.HtmlDecode(await loginPage.Content.ReadAsStringAsync());
            Assert.Contains("Yeni parolanızla giriş yapabilirsiniz", html, StringComparison.Ordinal);
            Assert.DoesNotContain("/admin/login", html, StringComparison.Ordinal);
            Assert.DoesNotContain("/superadmin/login", html, StringComparison.Ordinal);
        }
        using var loginClient = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(
            loginClient,
            "/account",
            AeternumWebApplicationFactory.ResetEmail,
            password: newPassword)).StatusCode);
    }

    [Fact]
    public async Task Forgot_password_redirects_with_one_privacy_safe_one_shot_success_flash()
    {
        using var client = factory.CreateClientWithoutRedirects();

        var response = await FormClient.PostFormAsync(
            client,
            "/account/forgot-password",
            "/account/forgot-password",
            new Dictionary<string, string> { ["Email"] = AeternumWebApplicationFactory.CustomerEmail });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/account/login", response.Headers.Location?.OriginalString);
        Assert.Contains(
            factory.Services.GetRequiredService<InMemoryIdentityMessageSender>().Messages,
            message => message.Subject.Contains("sıfırlayın", StringComparison.Ordinal));

        using var firstLoginPage = await client.GetAsync(response.Headers.Location);
        var firstLoginHtml = await firstLoginPage.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, firstLoginPage.StatusCode);
        Assert.Equal(1, firstLoginHtml.Split("data-server-flash-message", StringSplitOptions.None).Length - 1);
        Assert.Contains("data-server-flash-kind=\"success\"", firstLoginHtml, StringComparison.Ordinal);
        Assert.Contains(
            "Hesap uygunsa parola sıfırlama bağlantısı gönderildi.",
            WebUtility.HtmlDecode(firstLoginHtml),
            StringComparison.Ordinal);

        using var secondLoginPage = await client.GetAsync("/account/login");
        var secondLoginHtml = await secondLoginPage.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, secondLoginPage.StatusCode);
        Assert.DoesNotContain("data-server-flash-message", secondLoginHtml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Management_responses_are_not_cached()
    {
        using var client = factory.CreateClientWithoutRedirects();

        var response = await client.GetAsync("/admin/login");

        Assert.Contains("no-store", response.Headers.CacheControl?.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.True(response.Headers.Contains("X-Correlation-ID"));
    }

    [Fact]
    public async Task Correlation_id_accepts_safe_values_and_replaces_unsafe_values()
    {
        using var client = factory.CreateClientWithoutRedirects();
        using var safeRequest = new HttpRequestMessage(HttpMethod.Get, "/");
        safeRequest.Headers.TryAddWithoutValidation("X-Correlation-ID", "safe-trace_123");

        var safeResponse = await client.SendAsync(safeRequest);

        Assert.Equal("safe-trace_123", Assert.Single(safeResponse.Headers.GetValues("X-Correlation-ID")));

        using var unsafeRequest = new HttpRequestMessage(HttpMethod.Get, "/");
        unsafeRequest.Headers.TryAddWithoutValidation("X-Correlation-ID", "unsafe trace value");

        var unsafeResponse = await client.SendAsync(unsafeRequest);
        var replacement = Assert.Single(unsafeResponse.Headers.GetValues("X-Correlation-ID"));

        Assert.NotEqual("unsafe trace value", replacement);
        Assert.Matches("^[a-f0-9]{32}$", replacement);
    }

    [Fact]
    public async Task Security_stamp_change_deletes_the_customer_authentication_cookie()
    {
        var email = $"stamp-{Guid.NewGuid():N}@test.local";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = "Stamp",
                LastName = "Test",
                IsActive = true,
                CreatedAtUtc = factory.Clock.GetUtcNow(),
            };
            Assert.True((await userManager.CreateAsync(user, AeternumWebApplicationFactory.Password)).Succeeded);
            Assert.True((await userManager.AddToRoleAsync(user, RoleNames.Customer)).Succeeded);
        }

        using var client = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(client, "/account", email)).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = Assert.IsType<ApplicationUser>(await userManager.FindByEmailAsync(email));
            Assert.True((await userManager.UpdateSecurityStampAsync(user)).Succeeded);
        }

        var response = await client.GetAsync("/account");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var deletionCookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("AETKAHVE.Customer.Auth=", StringComparison.Ordinal));
        Assert.Contains("expires=", deletionCookie, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindAuthenticationCookie(HttpResponseMessage response, string cookieName) =>
        response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith(cookieName, StringComparison.Ordinal));

    private async Task AssertCustomerDoesNotExistAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Null(await userManager.FindByEmailAsync(email));
    }

    private static string ExtractHiddenValue(string html, string name)
    {
        var input = Regex.Match(
            html,
            $"<input[^>]*name=\"{Regex.Escape(name)}\"[^>]*>",
            RegexOptions.IgnoreCase);
        Assert.True(input.Success, $"Hidden input '{name}' was not rendered.");
        var value = Regex.Match(input.Value, "value=\"([^\"]*)\"", RegexOptions.IgnoreCase);
        Assert.True(value.Success, $"Hidden input '{name}' has no value.");
        return WebUtility.HtmlDecode(value.Groups[1].Value);
    }
}
