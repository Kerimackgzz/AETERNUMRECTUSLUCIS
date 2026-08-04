using System.Net;
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
}

