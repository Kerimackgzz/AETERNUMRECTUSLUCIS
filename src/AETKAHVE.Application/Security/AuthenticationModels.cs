namespace AETKAHVE.Application.Security;

public enum AuthenticationPortal
{
    Customer = 1,
    Admin = 2,
    SuperAdmin = 3,
}

public enum SignInStatus
{
    Succeeded = 1,
    Failed = 2,
    LockedOut = 3,
}

public sealed record SignInAttempt(
    string Email,
    string Password,
    bool RememberMe,
    AuthenticationPortal Portal,
    string? IpAddress,
    string? UserAgent,
    string? Route,
    string? CorrelationId);

public sealed record SignInOutcome(SignInStatus Status)
{
    public bool Succeeded => Status == SignInStatus.Succeeded;
}

public sealed record IdleSessionStatus(
    bool IsAuthenticated,
    DateTimeOffset ServerTimeUtc,
    DateTimeOffset? ExpiresAtUtc,
    int RemainingSeconds,
    int WarningSeconds);

