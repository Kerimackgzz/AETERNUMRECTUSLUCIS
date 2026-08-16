namespace AETKAHVE.Application.Security;

public sealed record BeginCustomerRegistration(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    DateTimeOffset PrivacyAcceptedAtUtc);

public sealed record RegistrationDispatch(Guid RegistrationId, string Email, string Token);

public enum RegistrationStartStatus
{
    Started,
    ExistingAccount,
    InvalidInput,
}

public sealed record RegistrationStartResult(
    RegistrationStartStatus Status,
    RegistrationDispatch? Dispatch);

public sealed record RegistrationValidationResult(bool CanConfirm, string? MaskedEmail);

public enum RegistrationCompletionStatus
{
    Completed,
    InvalidOrExpired,
    AlreadyCompleted,
    Unavailable,
}

public sealed record SecurityEventContext(
    string? IpAddress,
    string? UserAgent,
    string? Route,
    string? CorrelationId);

public interface ICustomerRegistrationService
{
    Task<RegistrationStartResult> BeginAsync(
        BeginCustomerRegistration request,
        CancellationToken cancellationToken = default);

    Task<RegistrationDispatch?> ResendAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<RegistrationValidationResult> ValidateConfirmationAsync(
        Guid registrationId,
        string token,
        CancellationToken cancellationToken = default);

    Task<RegistrationCompletionStatus> CompleteAsync(
        Guid registrationId,
        string token,
        SecurityEventContext context,
        CancellationToken cancellationToken = default);
}

public interface ICustomerPasswordResetService
{
    Task<bool> ResetAsync(
        string email,
        string token,
        string newPassword,
        SecurityEventContext context,
        CancellationToken cancellationToken = default);
}
