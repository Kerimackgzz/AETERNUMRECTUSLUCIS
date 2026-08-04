using AETKAHVE.Application.Security;
using AETKAHVE.Infrastructure.Identity;
using AETKAHVE.Infrastructure.Options;
using AETKAHVE.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Infrastructure.Security;

public sealed record ManagementSessionValidation(bool IsValid, bool IsIdleExpired, ManagementSession? Session);

public sealed class ManagementSessionService(
    AppDbContext dbContext,
    IOptions<SecurityOptions> securityOptions,
    TimeProvider timeProvider)
{
    private readonly SecurityOptions _securityOptions = securityOptions.Value;

    public async Task<ManagementSession> CreateAsync(
        ApplicationUser user,
        AuthenticationPortal portal,
        CancellationToken cancellationToken = default)
    {
        if (portal == AuthenticationPortal.Customer)
        {
            throw new ArgumentOutOfRangeException(nameof(portal), "Customer sessions are not management sessions.");
        }

        var now = timeProvider.GetUtcNow();
        var absoluteLifetime = ManagementSessionPolicy.AbsoluteLifetime(_securityOptions, portal);

        var session = new ManagementSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Portal = portal,
            SecurityStamp = user.SecurityStamp ?? string.Empty,
            CreatedAtUtc = now,
            LastActivityAtUtc = now,
            AbsoluteExpiresAtUtc = now.Add(absoluteLifetime),
        };

        dbContext.ManagementSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<ManagementSessionValidation> ValidateAsync(
        Guid sessionId,
        ApplicationUser user,
        AuthenticationPortal portal,
        bool touchActivity,
        CancellationToken cancellationToken = default)
    {
        var session = await dbContext.ManagementSessions
            .SingleOrDefaultAsync(x => x.Id == sessionId && x.UserId == user.Id && x.Portal == portal, cancellationToken);

        if (session is null || session.RevokedAtUtc.HasValue ||
            !string.Equals(session.SecurityStamp, user.SecurityStamp, StringComparison.Ordinal))
        {
            return new ManagementSessionValidation(false, false, session);
        }

        var now = timeProvider.GetUtcNow();
        var idleExpired = ManagementSessionPolicy.IsIdleExpired(session, _securityOptions, now);
        var absoluteExpired = now >= session.AbsoluteExpiresAtUtc;

        if (idleExpired || absoluteExpired)
        {
            session.RevokedAtUtc = now;
            session.RevocationReason = idleExpired ? "IdleTimeout" : "AbsoluteExpiration";
            session.ConcurrencyToken = Guid.NewGuid();
            await dbContext.SaveChangesAsync(cancellationToken);
            return new ManagementSessionValidation(false, idleExpired, session);
        }

        if (touchActivity)
        {
            session.LastActivityAtUtc = now;
            session.ConcurrencyToken = Guid.NewGuid();
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new ManagementSessionValidation(true, false, session);
    }

    public async Task RevokeAsync(Guid sessionId, string reason, CancellationToken cancellationToken = default)
    {
        var session = await dbContext.ManagementSessions.SingleOrDefaultAsync(x => x.Id == sessionId, cancellationToken);
        if (session is null || session.RevokedAtUtc.HasValue)
        {
            return;
        }

        session.RevokedAtUtc = timeProvider.GetUtcNow();
        session.RevocationReason = reason.Length <= 200 ? reason : reason[..200];
        session.ConcurrencyToken = Guid.NewGuid();
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public IdleSessionStatus ToStatus(ManagementSession session)
    {
        return ManagementSessionPolicy.CreateStatus(session, _securityOptions, timeProvider.GetUtcNow());
    }
}
