using System.Net;
using System.Text.RegularExpressions;

namespace AETKAHVE.IntegrationTests.Infrastructure;

public static partial class FormClient
{
    public static async Task<(HttpResponseMessage Response, string Token)> GetFormAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        var match = AntiforgeryTokenRegex().Match(html);
        Assert.True(match.Success, $"Antiforgery token was not found at {path}.");
        return (response, WebUtility.HtmlDecode(match.Groups[1].Value));
    }

    public static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client,
        string getPath,
        string postPath,
        IDictionary<string, string> values)
    {
        var (_, token) = await GetFormAsync(client, getPath);
        return await PostWithTokenAsync(client, postPath, token, values);
    }

    public static Task<HttpResponseMessage> PostWithTokenAsync(
        HttpClient client,
        string postPath,
        string token,
        IDictionary<string, string> values)
    {
        var form = new Dictionary<string, string>(values)
        {
            ["__RequestVerificationToken"] = token,
        };
        return client.PostAsync(postPath, new FormUrlEncodedContent(form));
    }

    public static Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string portalPath,
        string email,
        bool rememberMe = false,
        string password = AeternumWebApplicationFactory.Password) =>
        PostFormAsync(
            client,
            $"{portalPath}/login",
            $"{portalPath}/login",
            new Dictionary<string, string>
            {
                ["Email"] = email,
                ["Password"] = password,
                ["RememberMe"] = rememberMe.ToString(),
            });

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex AntiforgeryTokenRegex();
}

