using AETKAHVE.Infrastructure.Options;

namespace AETKAHVE.UnitTests;

public sealed class ProductionSecurityOptionsValidatorTests
{
    private readonly ForwardedHeadersSecurityOptionsValidator _forwardedHeadersValidator = new();

    [Fact]
    public void Forwarded_headers_are_safely_disabled_by_default()
    {
        var result = _forwardedHeadersValidator.Validate(null, new ForwardedHeadersSecurityOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Forwarded_headers_require_an_explicit_allow_list_when_enabled()
    {
        var result = _forwardedHeadersValidator.Validate(
            null,
            new ForwardedHeadersSecurityOptions { Enabled = true });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("explicit", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("not-an-ip")]
    public void Forwarded_headers_reject_wildcard_or_invalid_proxies(string proxy)
    {
        var result = _forwardedHeadersValidator.Validate(
            null,
            new ForwardedHeadersSecurityOptions
            {
                Enabled = true,
                KnownProxies = [proxy],
            });

        Assert.True(result.Failed);
    }

    [Fact]
    public void Production_requires_persistent_encrypted_data_protection_keys()
    {
        var validator = new DataProtectionKeyRingOptionsValidator(isProduction: true);

        var result = validator.Validate(null, new DataProtectionKeyRingOptions());

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("KeyRingPath", StringComparison.Ordinal));
        Assert.Contains(result.Failures, failure => failure.Contains("Certificate", StringComparison.Ordinal));
    }

    [Fact]
    public void Production_accepts_absolute_key_ring_and_certificate_configuration()
    {
        var validator = new DataProtectionKeyRingOptionsValidator(isProduction: true);
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "aetkahve-options-tests"));

        var result = validator.Validate(
            null,
            new DataProtectionKeyRingOptions
            {
                ApplicationName = "AETKAHVE",
                KeyRingPath = Path.Combine(root, "keys"),
                CertificateThumbprint = new string('A', 40),
            });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Non_production_can_use_platform_default_data_protection_storage()
    {
        var validator = new DataProtectionKeyRingOptionsValidator(isProduction: false);

        var result = validator.Validate(null, new DataProtectionKeyRingOptions());

        Assert.True(result.Succeeded);
    }
}
