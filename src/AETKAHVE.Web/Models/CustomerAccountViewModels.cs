using System.ComponentModel.DataAnnotations;
using AETKAHVE.Application.Commerce;
using Microsoft.AspNetCore.Http;

namespace AETKAHVE.Web.Models;

public sealed class CustomerAccountPageViewModel
{
    public required CustomerAccountDashboard Dashboard { get; init; }
    public CustomerProfileUpdateInput Profile { get; init; } = new();
    public CustomerEmailChangeInput EmailChange { get; init; } = new();
    public CustomerPasswordChangeInput PasswordChange { get; init; } = new();
}

public sealed class CustomerProfileUpdateInput
{
    [Required, StringLength(100)]
    [Display(Name = "Ad")]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(100)]
    [Display(Name = "Soyad")]
    public string LastName { get; set; } = string.Empty;

    [Phone, StringLength(30)]
    [Display(Name = "Telefon")]
    public string? PhoneNumber { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Doğum tarihi")]
    public DateOnly? DateOfBirth { get; set; }
}

public sealed class CustomerProfilePhotoInput
{
    [Required]
    public IFormFile? Photo { get; set; }
}

public sealed class CustomerEmailChangeInput
{
    [Required, DataType(DataType.Password)]
    [Display(Name = "Mevcut parola")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(320)]
    [Display(Name = "Yeni e-posta")]
    public string NewEmail { get; set; } = string.Empty;
}

public sealed class CustomerEmailChangeConfirmViewModel
{
    public string NewEmail { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public string MaskedEmail { get; init; } = string.Empty;
    public bool CanConfirm { get; init; }
    public string? StatusMessage { get; init; }
}

public sealed class CustomerPasswordChangeInput
{
    [Required, DataType(DataType.Password)]
    [Display(Name = "Mevcut parola")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), StringLength(128, MinimumLength = 12)]
    [Display(Name = "Yeni parola")]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Compare(nameof(NewPassword))]
    [Display(Name = "Yeni parola tekrarı")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
