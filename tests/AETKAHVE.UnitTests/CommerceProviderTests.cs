using AETKAHVE.Application.Commerce;
using AETKAHVE.Infrastructure.Commerce;
using AETKAHVE.Infrastructure.DependencyInjection;
using AETKAHVE.Infrastructure.Options;
using AETKAHVE.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using PdfSharp.Pdf.IO;

namespace AETKAHVE.UnitTests;

public sealed class CommerceProviderTests
{
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
}
