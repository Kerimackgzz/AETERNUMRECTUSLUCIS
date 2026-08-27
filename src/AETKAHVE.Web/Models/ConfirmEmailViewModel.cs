namespace AETKAHVE.Web.Models;

public sealed class ConfirmEmailViewModel
{
    public Guid RegistrationId { get; init; }

    public string Token { get; init; } = string.Empty;

    public bool CanConfirm { get; init; }

    public string? MaskedEmail { get; init; }

    public string? StatusMessage { get; init; }

    public string? ReturnUrl { get; init; }
}
