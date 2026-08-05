using System.ComponentModel.DataAnnotations;

namespace AETKAHVE.Infrastructure.Options;

public sealed class DataProtectionKeyRingOptions
{
    public const string SectionName = "DataProtection";

    [Required]
    public string ApplicationName { get; set; } = "AETKAHVE";

    [Required]
    public string KeyRingPath { get; set; } = "App_Data/data-protection-keys";
}
