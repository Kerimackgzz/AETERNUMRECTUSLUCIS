using System.ComponentModel.DataAnnotations;

namespace AETKAHVE.Web.Models;

public sealed record ManagementSecurityDetails(
    Guid UserId,
    string DisplayName,
    string Email,
    DateTimeOffset? LastLoginAtUtc,
    IReadOnlyList<string> Roles);

public sealed class ManagementSecurityViewModel
{
    public required ManagementSecurityDetails Details { get; init; }
    public required string PortalName { get; init; }
    public required string BasePath { get; init; }
    public string? ErrorSection { get; init; }
    public ManagementEmailChangeInput EmailChange { get; init; } = new();
    public ManagementPasswordChangeInput PasswordChange { get; init; } = new();
}

public sealed class ManagementEmailChangeInput
{
    [Required(ErrorMessage = "Mevcut parola zorunludur.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mevcut parola")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni e-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    [StringLength(320)]
    [Display(Name = "Yeni e-posta")]
    public string NewEmail { get; set; } = string.Empty;
}

public sealed class ManagementPasswordChangeInput
{
    [Required(ErrorMessage = "Mevcut parola zorunludur.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mevcut parola")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parola zorunludur.")]
    [DataType(DataType.Password)]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "Parola en az 12 karakter olmalıdır.")]
    [Display(Name = "Yeni parola")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni parola tekrarı zorunludur.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Parolalar eşleşmiyor.")]
    [Display(Name = "Yeni parola tekrarı")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class ManagementEmailChangeConfirmViewModel
{
    public Guid UserId { get; init; }
    public string NewEmail { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public string MaskedEmail { get; init; } = string.Empty;
    public bool CanConfirm { get; init; }
    public string? StatusMessage { get; init; }
    public string PortalName { get; init; } = string.Empty;
    public string ConfirmationPath { get; init; } = string.Empty;
    public string LoginPath { get; init; } = string.Empty;
}
