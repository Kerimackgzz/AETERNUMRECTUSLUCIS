namespace AETKAHVE.Application.Security;

public enum AdminAccountStatusFilter
{
    All = 0,
    Active = 1,
    Inactive = 2,
    PendingInvitation = 3,
    Locked = 4,
}

public sealed record AdminAccountQuery(
    string? Search = null,
    AdminAccountStatusFilter Status = AdminAccountStatusFilter.All,
    int Page = 1,
    int PageSize = 25);

public sealed record AdminAccountSummary(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    bool IsActive,
    bool IsPendingInvitation,
    bool IsLocked,
    DateTimeOffset? LockoutEndUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastLoginAtUtc);

public sealed record AdminAccountDetails(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    bool IsActive,
    bool IsPendingInvitation,
    bool IsLocked,
    DateTimeOffset? LockoutEndUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastLoginAtUtc);

public sealed record AdminAccountPage(
    IReadOnlyList<AdminAccountSummary> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record CreateAdminAccount(string FirstName, string LastName, string Email);

public sealed record UpdateAdminAccount(string FirstName, string LastName, string Email);

public sealed record AdminAccountOperationResult(bool Succeeded, string Message)
{
    public static AdminAccountOperationResult Success(string message) => new(true, message);

    public static AdminAccountOperationResult Failure(string message) => new(false, message);
}

public enum AdminAccountTokenPurpose
{
    Invitation = 1,
    PasswordReset = 2,
}

public sealed record AdminAccountTokenDispatch(
    Guid UserId,
    string Email,
    string Token,
    AdminAccountTokenPurpose Purpose);

public sealed record AdminEmailChangeDispatch(Guid UserId, string NewEmail, string Token);

public sealed record AdminAccountStartResult(
    AdminAccountOperationResult Result,
    AdminAccountTokenDispatch? Dispatch = null);

public sealed record AdminAccountUpdateResult(
    AdminAccountOperationResult Result,
    AdminEmailChangeDispatch? EmailChange = null);

public sealed record AdminEmailChangeCompletionResult(
    AdminAccountOperationResult Result,
    AdminAccountTokenDispatch? InvitationDispatch = null);

public sealed record AdminAccountTokenValidation(
    bool CanContinue,
    string MaskedEmail,
    bool IsActive);

public interface IAdminAccountManagementService
{
    Task<AdminAccountPage> SearchAsync(
        AdminAccountQuery query,
        CancellationToken cancellationToken = default);

    Task<AdminAccountDetails?> GetAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<AdminAccountStartResult> CreateAsync(
        Guid actorUserId,
        CreateAdminAccount input,
        SecurityEventContext context,
        CancellationToken cancellationToken = default);

    Task<AdminAccountUpdateResult> UpdateAsync(
        Guid actorUserId,
        Guid userId,
        UpdateAdminAccount input,
        SecurityEventContext context,
        CancellationToken cancellationToken = default);

    Task<AdminAccountStartResult> ResendInvitationAsync(
        Guid actorUserId,
        Guid userId,
        SecurityEventContext context,
        CancellationToken cancellationToken = default);

    Task<AdminAccountStartResult> BeginPasswordResetAsync(
        Guid actorUserId,
        Guid userId,
        SecurityEventContext context,
        CancellationToken cancellationToken = default);

    Task<AdminAccountOperationResult> QueueTokenEmailAsync(
        AdminAccountTokenDispatch dispatch,
        string callbackUrl,
        CancellationToken cancellationToken = default);

    Task<AdminAccountOperationResult> QueueEmailChangeAsync(
        AdminEmailChangeDispatch dispatch,
        string callbackUrl,
        CancellationToken cancellationToken = default);

    Task<AdminAccountTokenValidation> ValidateTokenAsync(
        Guid userId,
        string token,
        AdminAccountTokenPurpose purpose,
        CancellationToken cancellationToken = default);

    Task<AdminAccountOperationResult> CompleteInvitationAsync(
        Guid userId,
        string token,
        string password,
        SecurityEventContext context,
        CancellationToken cancellationToken = default);

    Task<AdminAccountOperationResult> CompletePasswordResetAsync(
        Guid userId,
        string token,
        string password,
        SecurityEventContext context,
        CancellationToken cancellationToken = default);

    Task<AdminAccountTokenValidation> ValidateEmailChangeAsync(
        Guid userId,
        string newEmail,
        string token,
        CancellationToken cancellationToken = default);

    Task<AdminEmailChangeCompletionResult> ConfirmEmailChangeAsync(
        Guid userId,
        string newEmail,
        string token,
        SecurityEventContext context,
        CancellationToken cancellationToken = default);

    Task<AdminAccountOperationResult> SetActiveAsync(
        Guid actorUserId,
        Guid userId,
        bool isActive,
        SecurityEventContext context,
        CancellationToken cancellationToken = default);

    Task<AdminAccountOperationResult> UnlockAsync(
        Guid actorUserId,
        Guid userId,
        SecurityEventContext context,
        CancellationToken cancellationToken = default);

    Task<AdminAccountOperationResult> DeleteAsync(
        Guid actorUserId,
        Guid userId,
        SecurityEventContext context,
        CancellationToken cancellationToken = default);
}
