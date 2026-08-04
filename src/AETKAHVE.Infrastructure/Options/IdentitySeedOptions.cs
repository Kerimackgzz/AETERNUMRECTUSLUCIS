namespace AETKAHVE.Infrastructure.Options;

public sealed class IdentitySeedOptions
{
    public const string SectionName = "IdentitySeed";

    public bool Enabled { get; set; }

    public string? AdminEmail { get; set; }

    public string? AdminPassword { get; set; }

    public string? SuperAdminEmail { get; set; }

    public string? SuperAdminPassword { get; set; }
}

