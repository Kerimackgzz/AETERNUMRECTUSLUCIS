using System.Net;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Infrastructure.Options;

public sealed class ForwardedHeadersSecurityOptionsValidator : IValidateOptions<ForwardedHeadersSecurityOptions>
{
    public ValidateOptionsResult Validate(string? name, ForwardedHeadersSecurityOptions options)
    {
        var failures = new List<string>();

        if (options.ForwardLimit is < 1 or > 10)
        {
            failures.Add("ForwardedHeaders:ForwardLimit must be between 1 and 10.");
        }

        var knownProxies = options.KnownProxies ?? [];
        var knownNetworks = options.KnownNetworks ?? [];
        ValidateProxies(knownProxies, failures);
        ValidateNetworks(knownNetworks, failures);

        var hasAllowList = knownProxies.Length > 0 || knownNetworks.Length > 0;
        if (options.Enabled && !hasAllowList)
        {
            failures.Add(
                "ForwardedHeaders requires at least one explicit KnownProxies or KnownNetworks entry when enabled.");
        }
        else if (!options.Enabled && hasAllowList)
        {
            failures.Add(
                "ForwardedHeaders allow-list entries require ForwardedHeaders:Enabled=true; otherwise remove them.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateProxies(IEnumerable<string> proxies, ICollection<string> failures)
    {
        foreach (var value in proxies)
        {
            if (!IPAddress.TryParse(value, out var address))
            {
                failures.Add($"ForwardedHeaders:KnownProxies contains invalid IP address '{value}'.");
                continue;
            }

            if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
            {
                failures.Add("ForwardedHeaders:KnownProxies cannot trust an unspecified/wildcard address.");
            }
        }
    }

    private static void ValidateNetworks(IEnumerable<string> networks, ICollection<string> failures)
    {
        foreach (var value in networks)
        {
            if (!IPNetwork.TryParse(value, out var network))
            {
                failures.Add($"ForwardedHeaders:KnownNetworks contains invalid CIDR network '{value}'.");
                continue;
            }

            if (network.PrefixLength == 0)
            {
                failures.Add("ForwardedHeaders:KnownNetworks cannot trust an all-addresses /0 network.");
            }
        }
    }
}

public sealed class DataProtectionKeyRingOptionsValidator(bool isProduction)
    : IValidateOptions<DataProtectionKeyRingOptions>
{
    public ValidateOptionsResult Validate(string? name, DataProtectionKeyRingOptions options)
    {
        var failures = new List<string>();
        var hasKeyRingPath = !string.IsNullOrWhiteSpace(options.KeyRingPath);
        var hasThumbprint = !string.IsNullOrWhiteSpace(options.CertificateThumbprint);
        var hasCertificatePath = !string.IsNullOrWhiteSpace(options.CertificatePath);
        var hasCertificatePassword = !string.IsNullOrWhiteSpace(options.CertificatePassword);

        if (string.IsNullOrWhiteSpace(options.ApplicationName) || options.ApplicationName.Length > 128)
        {
            failures.Add("DataProtection:ApplicationName is required and cannot exceed 128 characters.");
        }

        if (hasKeyRingPath && !Path.IsPathFullyQualified(options.KeyRingPath!))
        {
            failures.Add("DataProtection:KeyRingPath must be an absolute path.");
        }

        if (hasThumbprint && hasCertificatePath)
        {
            failures.Add(
                "Configure only one Data Protection certificate source: CertificateThumbprint or CertificatePath.");
        }

        if (hasThumbprint && !IsValidThumbprint(options.CertificateThumbprint!))
        {
            failures.Add("DataProtection:CertificateThumbprint must contain 40 to 128 hexadecimal characters.");
        }

        if (hasCertificatePath && !Path.IsPathFullyQualified(options.CertificatePath!))
        {
            failures.Add("DataProtection:CertificatePath must be an absolute path.");
        }

        if (hasCertificatePath && !hasCertificatePassword)
        {
            failures.Add(
                "DataProtection:CertificatePassword is required when CertificatePath is used; supply it through a secret provider.");
        }

        if (!hasCertificatePath && hasCertificatePassword)
        {
            failures.Add("DataProtection:CertificatePassword cannot be set without CertificatePath.");
        }

        if (!hasKeyRingPath && (hasThumbprint || hasCertificatePath || hasCertificatePassword))
        {
            failures.Add("A Data Protection certificate requires DataProtection:KeyRingPath.");
        }

        if (isProduction)
        {
            if (!hasKeyRingPath)
            {
                failures.Add(
                    "Production requires DataProtection:KeyRingPath so authentication and antiforgery keys survive restarts.");
            }

            if (!hasThumbprint && !hasCertificatePath)
            {
                failures.Add(
                    "Production requires DataProtection:CertificateThumbprint or CertificatePath to encrypt persisted keys at rest.");
            }
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsValidThumbprint(string value)
    {
        var normalized = string.Concat(value.Where(character => !char.IsWhiteSpace(character)));
        return normalized.Length is >= 40 and <= 128 && normalized.All(Uri.IsHexDigit);
    }
}
