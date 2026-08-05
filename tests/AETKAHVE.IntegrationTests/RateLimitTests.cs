using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using AETKAHVE.IntegrationTests.Infrastructure;

namespace AETKAHVE.IntegrationTests;

public sealed class RateLimitTests(AeternumWebApplicationFactory factory)
    : IClassFixture<AeternumWebApplicationFactory>
{
    [Fact]
    public async Task Admin_login_is_rate_limited()
    {
        using var client = factory.CreateClientWithoutRedirects();
        var (_, token) = await FormClient.GetFormAsync(client, "/admin/login");
        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < 7; attempt++)
        {
            var response = await FormClient.PostWithTokenAsync(
                client,
                "/admin/login",
                token,
                new Dictionary<string, string>
                {
                    ["Email"] = AeternumWebApplicationFactory.AdminEmail,
                    ["Password"] = "IncorrectPassword1!",
                    ["RememberMe"] = "false",
                });
            statuses.Add(response.StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
    }

    [Fact]
    public async Task Customer_registration_is_rate_limited()
    {
        using var client = factory.CreateClientWithoutRedirects();
        var (_, token) = await FormClient.GetFormAsync(client, "/account/register");
        var statuses = new List<HttpStatusCode>();

        for (var attempt = 0; attempt < 6; attempt++)
        {
            var response = await FormClient.PostWithTokenAsync(
                client,
                "/account/register",
                token,
                new Dictionary<string, string>());
            statuses.Add(response.StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
    }

    [Fact]
    public async Task Password_recovery_is_rate_limited()
    {
        using var client = factory.CreateClientWithoutRedirects();
        var (_, token) = await FormClient.GetFormAsync(client, "/account/forgot-password");
        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? lastResponse = null;

        for (var attempt = 0; attempt < 6; attempt++)
        {
            lastResponse = await FormClient.PostWithTokenAsync(
                client,
                "/account/forgot-password",
                token,
                new Dictionary<string, string>
                {
                    ["Email"] = $"missing-{attempt}@test.local",
                });
            statuses.Add(lastResponse.StatusCode);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[^1]);
        Assert.NotNull(lastResponse);
        Assert.True(lastResponse.Headers.TryGetValues("Retry-After", out var values));
        Assert.True(int.TryParse(Assert.Single(values), out var retryAfterSeconds));
        Assert.InRange(retryAfterSeconds, 1, 60);
    }

    [Fact]
    public async Task Public_contact_submission_is_rate_limited()
    {
        using var client = factory.CreateClientWithoutRedirects();
        var page = await client.GetStringAsync("/contact");
        var tokenMatch = Regex.Match(
            page,
            "<meta name=\"csrf-token\" content=\"([^\"]+)\"",
            RegexOptions.IgnoreCase);
        Assert.True(tokenMatch.Success);

        HttpResponseMessage? lastResponse = null;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/contact")
            {
                Content = JsonContent.Create(new
                {
                    fullName = "Rate Limit Test",
                    email = $"contact-{attempt}@test.local",
                    phoneNumber = (string?)null,
                    subject = "Rate limit",
                    message = "Public contact abuse control integration test.",
                    privacyAccepted = true,
                }),
            };
            request.Headers.Add("RequestVerificationToken", tokenMatch.Groups[1].Value);
            lastResponse = await client.SendAsync(request);
        }

        Assert.NotNull(lastResponse);
        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse.StatusCode);
        Assert.True(lastResponse.Headers.TryGetValues("Retry-After", out var values));
        Assert.True(int.TryParse(Assert.Single(values), out var retryAfterSeconds));
        Assert.InRange(retryAfterSeconds, 1, 60);
    }
}

