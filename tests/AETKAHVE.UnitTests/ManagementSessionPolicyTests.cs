using AETKAHVE.Application.Security;
using AETKAHVE.Infrastructure.Options;
using AETKAHVE.Infrastructure.Security;

namespace AETKAHVE.UnitTests;

public sealed class ManagementSessionPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 4, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(AuthenticationPortal.Admin, 15)]
    [InlineData(AuthenticationPortal.SuperAdmin, 10)]
    public void Idle_timeout_uses_portal_specific_configuration(AuthenticationPortal portal, int expectedMinutes)
    {
        var timeout = ManagementSessionPolicy.IdleTimeout(new SecurityOptions(), portal);

        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), timeout);
    }

    [Fact]
    public void Idle_expiration_is_inclusive_at_the_boundary()
    {
        var session = CreateSession(AuthenticationPortal.Admin, Now.AddMinutes(-15));

        Assert.True(ManagementSessionPolicy.IsIdleExpired(session, new SecurityOptions(), Now));
    }

    [Fact]
    public void Status_uses_the_earlier_idle_or_absolute_expiration()
    {
        var session = CreateSession(AuthenticationPortal.Admin, Now.AddMinutes(-5));
        session.AbsoluteExpiresAtUtc = Now.AddMinutes(3);

        var status = ManagementSessionPolicy.CreateStatus(session, new SecurityOptions(), Now);

        Assert.Equal(180, status.RemainingSeconds);
        Assert.Equal(Now.AddMinutes(3), status.ExpiresAtUtc);
    }

    private static ManagementSession CreateSession(AuthenticationPortal portal, DateTimeOffset lastActivity) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Portal = portal,
        SecurityStamp = "stamp",
        CreatedAtUtc = Now.AddHours(-1),
        LastActivityAtUtc = lastActivity,
        AbsoluteExpiresAtUtc = Now.AddHours(1),
    };
}

