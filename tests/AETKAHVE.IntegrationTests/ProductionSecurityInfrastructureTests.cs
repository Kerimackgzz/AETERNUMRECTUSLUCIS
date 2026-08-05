using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using AETKAHVE.Infrastructure.DependencyInjection;
using AETKAHVE.Infrastructure.Options;
using AETKAHVE.Web.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AETKAHVE.IntegrationTests;

public sealed class ProductionSecurityInfrastructureTests
{
    private static readonly IPAddress ProxyAddress = IPAddress.Parse("203.0.113.10");

    [Fact]
    public async Task Trusted_proxy_uses_verified_forwarded_client_ip_for_rate_limit_partitions()
    {
        await using var app = await CreateRateLimitApplicationAsync(ProxyAddress.ToString());
        using var client = app.GetTestClient();

        using var firstClient = await SendForwardedRequestAsync(client, "198.51.100.1");
        using var secondClient = await SendForwardedRequestAsync(client, "198.51.100.2");
        using var repeatedFirstClient = await SendForwardedRequestAsync(client, "198.51.100.1");

        Assert.Equal(HttpStatusCode.OK, firstClient.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondClient.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, repeatedFirstClient.StatusCode);
    }

    [Fact]
    public async Task Untrusted_proxy_cannot_split_rate_limit_partition_with_forwarded_headers()
    {
        await using var app = await CreateRateLimitApplicationAsync("203.0.113.11");
        using var client = app.GetTestClient();

        using var firstSpoofedClient = await SendForwardedRequestAsync(client, "198.51.100.1");
        using var secondSpoofedClient = await SendForwardedRequestAsync(client, "198.51.100.2");

        Assert.Equal(HttpStatusCode.OK, firstSpoofedClient.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondSpoofedClient.StatusCode);
    }

    [Fact]
    public async Task Forwarded_headers_are_ignored_when_no_trusted_proxy_is_configured()
    {
        await using var app = await CreateRateLimitApplicationAsync(trustedProxy: null);
        using var client = app.GetTestClient();

        using var firstSpoofedClient = await SendForwardedRequestAsync(client, "198.51.100.1");
        using var secondSpoofedClient = await SendForwardedRequestAsync(client, "198.51.100.2");

        Assert.Equal(HttpStatusCode.OK, firstSpoofedClient.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondSpoofedClient.StatusCode);
    }

    [Fact]
    public void Missing_production_data_protection_configuration_fails_fast()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            services.AddWebSecurityModule(configuration, new TestHostEnvironment(Environments.Production)));

        Assert.Contains("DataProtection:KeyRingPath", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Certificate", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_data_protection_keys_are_persistent_and_certificate_encrypted()
    {
        var testRoot = Path.Combine(
            Path.GetTempPath(),
            $"aetkahve-data-protection-{Guid.NewGuid():N}");
        var keyRingPath = Path.Combine(testRoot, "keys");
        var certificatePath = Path.Combine(testRoot, "key-encryption.pfx");
        const string certificatePassword = "Integration-Test-Key-Password-1!";

        Directory.CreateDirectory(testRoot);
        try
        {
            CreateCertificate(certificatePath, certificatePassword);
            var configuration = CreateDataProtectionConfiguration(
                keyRingPath,
                certificatePath,
                certificatePassword);

            string protectedPayload;
            using (var firstProvider = CreateDataProtectionServices(configuration))
            {
                protectedPayload = firstProvider
                    .GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("production-security-integration-test")
                    .Protect("persistent-payload");
            }

            using (var secondProvider = CreateDataProtectionServices(configuration))
            {
                var unprotectedPayload = secondProvider
                    .GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("production-security-integration-test")
                    .Unprotect(protectedPayload);
                Assert.Equal("persistent-payload", unprotectedPayload);
            }

            var keyFile = Assert.Single(Directory.GetFiles(keyRingPath, "key-*.xml"));
            var keyXml = File.ReadAllText(keyFile);
            Assert.Contains("encryptedSecret", keyXml, StringComparison.Ordinal);
            Assert.DoesNotContain("<masterKey requiresEncryption=\"true\">\n      <value>", keyXml, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static async Task<WebApplication> CreateRateLimitApplicationAsync(string? trustedProxy)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ProductionSecurityInfrastructureTests).Assembly.FullName,
            EnvironmentName = "Testing",
        });
        builder.WebHost.UseTestServer();
        var securityConfiguration = new Dictionary<string, string?>
        {
            ["ForwardedHeaders:Enabled"] = (trustedProxy is not null).ToString(),
            ["ForwardedHeaders:ForwardLimit"] = "1",
        };
        if (trustedProxy is not null)
        {
            securityConfiguration["ForwardedHeaders:KnownProxies:0"] = trustedProxy;
        }

        builder.Configuration.AddInMemoryCollection(securityConfiguration);
        builder.Services.AddLogging();
        builder.Services.AddRouting();
        builder.Services.Configure<SecurityOptions>(options => options.AdminLoginRequestsPerMinute = 1);
        builder.Services.AddWebSecurityModule(builder.Configuration, builder.Environment);

        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.Connection.RemoteIpAddress = ProxyAddress;
            await next(context);
        });
        app.UseForwardedHeaders();
        app.UseRouting();
        app.UseRateLimiter();
        app.MapGet("/", () => "ok")
            .RequireRateLimiting(SecurityRateLimitPolicies.AdminLogin);

        await app.StartAsync();
        return app;
    }

    private static async Task<HttpResponseMessage> SendForwardedRequestAsync(HttpClient client, string clientIp)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", clientIp);
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        return await client.SendAsync(request);
    }

    private static IConfiguration CreateDataProtectionConfiguration(
        string keyRingPath,
        string certificatePath,
        string certificatePassword) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:ApplicationName"] = "AETKAHVE.IntegrationTests",
                ["DataProtection:KeyRingPath"] = keyRingPath,
                ["DataProtection:CertificatePath"] = certificatePath,
                ["DataProtection:CertificatePassword"] = certificatePassword,
            })
            .Build();

    private static ServiceProvider CreateDataProtectionServices(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddWebSecurityModule(configuration, new TestHostEnvironment(Environments.Production));
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static void CreateCertificate(string certificatePath, string password)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=AETKAHVE Integration Data Protection",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DataEncipherment,
            critical: true));
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(1));
        File.WriteAllBytes(certificatePath, certificate.Export(X509ContentType.Pkcs12, password));
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "AETKAHVE.IntegrationTests";

        public string ContentRootPath { get; set; } = Path.GetTempPath();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
