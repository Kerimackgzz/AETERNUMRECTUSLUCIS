using System.Security.Cryptography;
using System.Text;
using AETKAHVE.Application.Commerce;
using AETKAHVE.Infrastructure.Commerce;
using AETKAHVE.Infrastructure.DependencyInjection;
using AETKAHVE.Infrastructure.Options;
using AETKAHVE.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PdfSharp.Pdf.IO;

namespace AETKAHVE.UnitTests;

public sealed class CommerceProviderTests
{
    [Fact]
    public async Task Production_host_fails_start_when_mock_payment_is_configured()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Production,
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Payment:Provider"] = PaymentProviderNames.Mock,
        });
        builder.Services.AddSingleton<IValidateOptions<PaymentOptions>, PaymentOptionsValidator>();
        builder.Services.AddOptions<PaymentOptions>()
            .Bind(builder.Configuration.GetSection(PaymentOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains("restricted to Development and Testing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Production_host_fails_start_when_smtp_configuration_is_missing()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            EnvironmentName = Environments.Production,
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Notifications:UseMockProviders"] = "false",
        });
        builder.Services.AddSingleton<IValidateOptions<NotificationOptions>, NotificationOptionsValidator>();
        builder.Services.AddOptions<NotificationOptions>()
            .Bind(builder.Configuration.GetSection(NotificationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<SmtpOptions>, SmtpOptionsValidator>();
        builder.Services.AddOptions<SmtpOptions>()
            .Bind(builder.Configuration.GetSection(SmtpOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains("Smtp:Host is required", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Signed_webhook_accepts_one_fresh_event_and_rejects_its_replay()
    {
        var now = new DateTimeOffset(2026, 8, 5, 10, 0, 0, TimeSpan.Zero);
        var clock = new FixedTimeProvider(now);
        var secret = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        var verifier = new HmacSha256PaymentWebhookVerifier(
            "Acquirer",
            secret,
            TimeSpan.FromMinutes(5),
            clock,
            new InMemoryPaymentWebhookReplayStore());
        const string eventId = "evt-123";
        const string rawBody = "reference=payment-1&transactionId=transaction-1&status=success";
        var timestamp = now.ToUnixTimeSeconds();
        var signature = Convert.ToHexString(HMACSHA256.HashData(
            secret,
            Encoding.UTF8.GetBytes(HmacSha256PaymentWebhookVerifier.BuildCanonicalPayload("Acquirer", eventId, timestamp, rawBody))));
        var envelope = CreateSignedEnvelope(eventId, timestamp, signature, rawBody);

        var first = await verifier.AuthenticateAsync(envelope, default);
        var replay = await verifier.AuthenticateAsync(envelope, default);

        Assert.True(first.Succeeded);
        Assert.False(replay.Succeeded);
        Assert.Equal("REPLAYED_EVENT", replay.ResponseCode);
    }

    [Theory]
    [InlineData(-301, false, "STALE_TIMESTAMP")]
    [InlineData(301, false, "STALE_TIMESTAMP")]
    [InlineData(0, true, "INVALID_SIGNATURE")]
    public async Task Signed_webhook_rejects_stale_future_and_tampered_deliveries(
        int timestampOffsetSeconds,
        bool tamperBody,
        string expectedResponseCode)
    {
        var now = new DateTimeOffset(2026, 8, 5, 10, 0, 0, TimeSpan.Zero);
        var secret = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        var verifier = new HmacSha256PaymentWebhookVerifier(
            "Acquirer",
            secret,
            TimeSpan.FromMinutes(5),
            new FixedTimeProvider(now),
            new InMemoryPaymentWebhookReplayStore());
        const string eventId = "evt-security-boundary";
        const string signedBody = "reference=payment-1&transactionId=transaction-1&status=success";
        var timestamp = now.AddSeconds(timestampOffsetSeconds).ToUnixTimeSeconds();
        var signature = Convert.ToHexString(HMACSHA256.HashData(
            secret,
            Encoding.UTF8.GetBytes(HmacSha256PaymentWebhookVerifier.BuildCanonicalPayload("Acquirer", eventId, timestamp, signedBody))));
        var deliveredBody = tamperBody ? signedBody.Replace("success", "fail", StringComparison.Ordinal) : signedBody;

        var result = await verifier.AuthenticateAsync(
            CreateSignedEnvelope(eventId, timestamp, signature, deliveredBody),
            default);

        Assert.False(result.Succeeded);
        Assert.Equal(expectedResponseCode, result.ResponseCode);
    }

    [Fact]
    public async Task Mock_webhook_verifier_rejects_production_even_if_resolved_directly()
    {
        var verifier = new MockPaymentWebhookVerifier(new TestHostEnvironment("Production"));
        var envelope = new PaymentWebhookEnvelope(
            PaymentProviderNames.Mock,
            "POST",
            new PaymentCallbackRequest("mock_reference", "transaction", "success"),
            "reference=mock_reference&transactionId=transaction&status=success",
            new Dictionary<string, string>());

        var result = await verifier.AuthenticateAsync(envelope, default);

        Assert.False(result.Succeeded);
        Assert.Equal("MOCK_NOT_ALLOWED", result.ResponseCode);
    }

    [Theory]
    [InlineData("Production", PaymentProviderNames.Mock, false)]
    [InlineData("Staging", PaymentProviderNames.Mock, false)]
    [InlineData("Development", PaymentProviderNames.Mock, true)]
    [InlineData("Testing", PaymentProviderNames.Mock, true)]
    [InlineData("Production", PaymentProviderNames.Disabled, true)]
    [InlineData("Production", "Unregistered", false)]
    public void Payment_provider_validation_fails_closed_for_unsafe_environment_combinations(
        string environmentName,
        string providerName,
        bool expectedSuccess)
    {
        var validator = new PaymentOptionsValidator(new TestHostEnvironment(environmentName));

        var result = validator.Validate(null, new PaymentOptions { Provider = providerName });

        Assert.Equal(expectedSuccess, result.Succeeded);
    }

    [Theory]
    [InlineData("SqlServer", "Server=(localdb)\\mssqllocaldb;Database=ProviderTest;Trusted_Connection=True", "Microsoft.EntityFrameworkCore.SqlServer")]
    [InlineData("Sqlite", "Data Source=:memory:", "Microsoft.EntityFrameworkCore.Sqlite")]
    public void Infrastructure_module_selects_the_configured_database_provider(
        string configuredProvider,
        string connectionString,
        string expectedProvider)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = configuredProvider,
            ["Database:ConnectionString"] = connectionString,
        }).Build();
        var services = new ServiceCollection();
        services.AddInfrastructureModule(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Assert.Equal(expectedProvider, db.Database.ProviderName);
    }

    [Fact]
    public async Task Mock_payment_gateway_verifies_initialized_amount_and_status()
    {
        var gateway = new MockPaymentGateway();
        var paymentId = Guid.NewGuid();
        var initialized = await gateway.InitializeAsync(new PaymentRequest(paymentId, Guid.NewGuid(), 125.50m, "TRY", "idem", "success", "https://localhost/callback"), default);
        var verified = await gateway.VerifyAsync(new PaymentCallbackRequest(initialized.RequestReference, "tx-1", "success"), default);
        Assert.True(initialized.Succeeded);
        Assert.True(verified.Succeeded);
        Assert.Equal(125.50m, verified.Amount);
        Assert.Equal("TRY", verified.Currency);
    }

    [Fact]
    public async Task Mock_payment_gateway_supports_failure_cancel_and_refund_failure()
    {
        var gateway = new MockPaymentGateway();
        var initialized = await gateway.InitializeAsync(new PaymentRequest(Guid.NewGuid(), Guid.NewGuid(), 10m, "TRY", "idem", "success", "https://localhost/callback"), default);
        Assert.False((await gateway.VerifyAsync(new PaymentCallbackRequest(initialized.RequestReference, "tx", "fail"), default)).Succeeded);
        Assert.True((await gateway.VerifyAsync(new PaymentCallbackRequest(initialized.RequestReference, "tx", "cancel"), default)).Cancelled);
        Assert.False((await gateway.RefundAsync(new RefundRequest(Guid.NewGuid(), "refund-fail", 10m, "TRY", "return"), default)).Succeeded);
    }

    [Fact]
    public async Task Invoice_generator_creates_a_valid_multi_page_pdf()
    {
        var lines = Enumerable.Range(1, 100).Select(x => new InvoiceLine($"Coffee {x}", $"SKU-{x}", 1, 10, 0, 0, 10)).ToList();
        var document = new InvoiceDocument("INV-1", DateTimeOffset.UtcNow, "AETERNUM", "ORDER-1", "Test Customer", "Test Address",
            lines, 1000, 0, 0, 0, 1000, "TRY");

        var bytes = await new InvoicePdfGenerator().GenerateAsync(document, default);

        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4), StringComparison.Ordinal);
        using var stream = new MemoryStream(bytes);
        using var pdf = PdfReader.Open(stream, PdfDocumentOpenMode.Import);
        Assert.True(pdf.PageCount >= 3);
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "AETKAHVE.UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static PaymentWebhookEnvelope CreateSignedEnvelope(
        string eventId,
        long timestamp,
        string signature,
        string rawBody) =>
        new(
            "Acquirer",
            "POST",
            new PaymentCallbackRequest("payment-1", "transaction-1", "success"),
            rawBody,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [PaymentWebhookHeaders.EventId] = eventId,
                [PaymentWebhookHeaders.Timestamp] = timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture),
                [PaymentWebhookHeaders.Signature] = "sha256=" + signature,
            });

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
