using System.ComponentModel.DataAnnotations;
using AETKAHVE.Application.Security;

namespace AETKAHVE.Web.Models;

public sealed class AdminAccountsPageViewModel
{
    public required AdminAccountPage Accounts { get; init; }

    public string? Search { get; init; }

    public AdminAccountStatusFilter Status { get; init; }
}

public class AdminAccountCreateViewModel
{
    [Required(ErrorMessage = "Ad zorunludur.")]
    [StringLength(100, ErrorMessage = "Ad en fazla 100 karakter olabilir.")]
    [Display(Name = "Ad")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Soyad zorunludur.")]
    [StringLength(100, ErrorMessage = "Soyad en fazla 100 karakter olabilir.")]
    [Display(Name = "Soyad")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "E-posta zorunludur.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi girin.")]
    [StringLength(320)]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;
}

public sealed class AdminAccountEditViewModel : AdminAccountCreateViewModel
{
    public Guid Id { get; set; }
}

public sealed class AdminAccountPasswordTokenViewModel
{
    public Guid UserId { get; set; }

    [Required]
    public string Token { get; set; } = string.Empty;

    public string MaskedEmail { get; set; } = string.Empty;

    public bool CanContinue { get; set; }

    public bool IsActive { get; set; }

    [Required(ErrorMessage = "Parola zorunludur.")]
    [StringLength(128, MinimumLength = 12, ErrorMessage = "Parola en az 12 karakter olmalıdır.")]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni parola")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Parola tekrarı zorunludur.")]
    [Compare(nameof(Password), ErrorMessage = "Parolalar eşleşmiyor.")]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni parola tekrarı")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class AdminAccountEmailChangeViewModel
{
    public Guid UserId { get; set; }

    public string NewEmail { get; set; } = string.Empty;

    public string Token { get; set; } = string.Empty;

    public string MaskedEmail { get; set; } = string.Empty;

    public bool CanConfirm { get; set; }

    public string? StatusMessage { get; set; }
}
