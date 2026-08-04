namespace AETKAHVE.Infrastructure.Security;

public sealed class AuditLog
{
    public long Id { get; set; }

    public Guid? AdminUserId { get; set; }

    public string ActionType { get; set; } = string.Empty;

    public string? EntityType { get; set; }

    public string? EntityId { get; set; }

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string? Route { get; set; }

    public string CorrelationId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

