using AETKAHVE.Application.Security;

namespace AETKAHVE.Infrastructure.Security;

public sealed class ManagementSession
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public AuthenticationPortal Portal { get; set; }

    public string SecurityStamp { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset LastActivityAtUtc { get; set; }

    public DateTimeOffset AbsoluteExpiresAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }

    public string? RevocationReason { get; set; }

    public Guid ConcurrencyToken { get; set; } = Guid.NewGuid();
}
