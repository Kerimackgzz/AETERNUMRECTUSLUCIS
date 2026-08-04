using AETKAHVE.Infrastructure.Persistence;

namespace AETKAHVE.Infrastructure.Security;

public sealed class SecurityAuditWriter(AppDbContext dbContext, TimeProvider timeProvider)
{
    public async Task WriteAsync(
        string actionType,
        string description,
        Guid? actorUserId,
        string? ipAddress,
        string? userAgent,
        string? route,
        string? correlationId,
        CancellationToken cancellationToken = default)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            AdminUserId = actorUserId,
            ActionType = Truncate(actionType, 100) ?? "SecurityEvent",
            Description = Truncate(description, 500) ?? "Security event recorded.",
            IpAddress = Truncate(ipAddress, 64),
            UserAgent = Truncate(userAgent, 512),
            Route = Truncate(route, 256),
            CorrelationId = Truncate(correlationId, 128) ?? Guid.NewGuid().ToString("N"),
            CreatedAtUtc = timeProvider.GetUtcNow(),
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string? Truncate(string? value, int maximumLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= maximumLength ? value : value[..maximumLength];
}

