namespace AETKAHVE.Infrastructure.Options;

public sealed class ForwardedHeadersSecurityOptions
{
    public const string SectionName = "ForwardedHeaders";

    public bool Enabled { get; set; }

    public int ForwardLimit { get; set; } = 1;

    public string[] KnownProxies { get; set; } = [];

    public string[] KnownNetworks { get; set; } = [];
}

public sealed class DataProtectionKeyRingOptions
{
    public const string SectionName = "DataProtection";

    public string ApplicationName { get; set; } = "AETKAHVE";

    public string? KeyRingPath { get; set; }

    public string? CertificateThumbprint { get; set; }

    public string? CertificatePath { get; set; }

    public string? CertificatePassword { get; set; }
}
