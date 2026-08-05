namespace AETKAHVE.Infrastructure.Identity;

public sealed class PendingCustomerRegistration
{
    public Guid Id { get; set; }

    public Guid ReservedUserId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string NormalizedEmail { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public byte[] VerificationTokenHash { get; set; } = [];

    public DateTimeOffset PrivacyAcceptedAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset LastEmailSentAtUtc { get; set; }

    public DateTimeOffset TokenExpiresAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
}
