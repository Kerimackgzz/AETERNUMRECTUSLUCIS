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
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync(confirmationUrl)).StatusCode);

        using var loginClient = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(
            loginClient,
            "/account",
            email)).StatusCode);
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
        using var loginClient = factory.CreateClientWithoutRedirects();
        Assert.Equal(HttpStatusCode.Redirect, (await FormClient.LoginAsync(
            loginClient,
            "/account",
            AeternumWebApplicationFactory.ResetEmail,
            password: newPassword)).StatusCode);
    }

    [Fact]
    public async Task Forgot_password_uses_the_mock_message_sender()
    {
        using var client = factory.CreateClientWithoutRedirects();

        var response = await FormClient.PostFormAsync(
            client,
            "/account/forgot-password",
            "/account/forgot-password",
            new Dictionary<string, string> { ["Email"] = AeternumWebApplicationFactory.CustomerEmail });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains(
            factory.Services.GetRequiredService<InMemoryIdentityMessageSender>().Messages,
            message => message.Subject.Contains("sıfırlayın", StringComparison.Ordinal));
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
}
