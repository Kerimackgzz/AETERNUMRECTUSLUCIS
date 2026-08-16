using System.Data;
using System.Security.Cryptography;
using System.Text;
using AETKAHVE.Application.Security;
using AETKAHVE.Infrastructure.Options;
using AETKAHVE.Infrastructure.Persistence;
using AETKAHVE.Infrastructure.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Infrastructure.Identity;

public sealed class CustomerRegistrationService(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IPasswordHasher<ApplicationUser> passwordHasher,
    IOptions<SecurityOptions> securityOptions,
    TimeProvider timeProvider) : ICustomerRegistrationService
{
    public async Task<RegistrationStartResult> BeginAsync(
        BeginCustomerRegistration request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var email = request.Email.Trim();
        var normalizedEmail = userManager.NormalizeEmail(email) ?? email.ToUpperInvariant();
        var now = timeProvider.GetUtcNow();
        var candidate = CreateCandidate(Guid.NewGuid(), request, email, now);

        // Register is an explicit account-existence check. Returning before password
        // validation also prevents an existing user from receiving a misleading
        // password-policy error and, importantly, never queues another message.
        if (await dbContext.Users.AnyAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            return new RegistrationStartResult(RegistrationStartStatus.ExistingAccount, null);
        }

        foreach (var validator in userManager.PasswordValidators)
        {
            var validation = await validator.ValidateAsync(userManager, candidate, request.Password);
            if (!validation.Succeeded)
            {
                return new RegistrationStartResult(RegistrationStartStatus.InvalidInput, null);
            }
        }

        var passwordHash = passwordHasher.HashPassword(candidate, request.Password);
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

            if (await dbContext.Users.AnyAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken))
            {
                await transaction.CommitAsync(cancellationToken);
                return new RegistrationStartResult(RegistrationStartStatus.ExistingAccount, null);
            }

            var pending = await dbContext.PendingCustomerRegistrations
                .SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
            var token = CreateToken();
            if (pending is null)
            {
                pending = new PendingCustomerRegistration
                {
                    Id = Guid.NewGuid(),
                    ReservedUserId = candidate.Id,
                    CreatedAtUtc = now,
                };
                dbContext.PendingCustomerRegistrations.Add(pending);
            }

            pending.FirstName = request.FirstName.Trim();
            pending.LastName = request.LastName.Trim();
            pending.Email = email;
            pending.NormalizedEmail = normalizedEmail;
            pending.PasswordHash = passwordHash;
            pending.VerificationTokenHash = HashToken(token);
            pending.PrivacyAcceptedAtUtc = request.PrivacyAcceptedAtUtc;
            pending.CreatedAtUtc = now;
            pending.LastEmailSentAtUtc = now;
            pending.TokenExpiresAtUtc = now.AddMinutes(securityOptions.Value.RegistrationConfirmationTokenMinutes);
            pending.ConcurrencyToken = Guid.NewGuid();

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new RegistrationStartResult(
                RegistrationStartStatus.Started,
                new RegistrationDispatch(pending.Id, pending.Email, token));
        });
    }

    public async Task<RegistrationDispatch?> ResendAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = userManager.NormalizeEmail(email.Trim()) ?? email.Trim().ToUpperInvariant();
        var now = timeProvider.GetUtcNow();
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            if (await dbContext.Users.AnyAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken))
            {
                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            var pending = await dbContext.PendingCustomerRegistrations
                .SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
            if (pending is null || pending.CreatedAtUtc < now.AddDays(-securityOptions.Value.PendingRegistrationRetentionDays))
            {
                if (pending is not null)
                {
                    dbContext.PendingCustomerRegistrations.Remove(pending);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                return null;
            }

            var token = CreateToken();
            pending.VerificationTokenHash = HashToken(token);
            pending.LastEmailSentAtUtc = now;
            pending.TokenExpiresAtUtc = now.AddMinutes(securityOptions.Value.RegistrationConfirmationTokenMinutes);
            pending.ConcurrencyToken = Guid.NewGuid();
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new RegistrationDispatch(pending.Id, pending.Email, token);
        });
    }

    public async Task<RegistrationValidationResult> ValidateConfirmationAsync(
        Guid registrationId,
        string token,
        CancellationToken cancellationToken = default)
    {
        var pending = await dbContext.PendingCustomerRegistrations
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == registrationId, cancellationToken);
        return IsValid(pending, token, timeProvider.GetUtcNow())
            ? new RegistrationValidationResult(true, MaskEmail(pending!.Email))
            : new RegistrationValidationResult(false, null);
    }

    public async Task<RegistrationCompletionStatus> CompleteAsync(
        Guid registrationId,
        string token,
        SecurityEventContext context,
        CancellationToken cancellationToken = default)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        try
        {
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                var pending = await dbContext.PendingCustomerRegistrations
                    .SingleOrDefaultAsync(x => x.Id == registrationId, cancellationToken);
                if (pending is null || !IsValid(pending, token, timeProvider.GetUtcNow()))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return RegistrationCompletionStatus.InvalidOrExpired;
                }

                if (await dbContext.Users.AnyAsync(x => x.NormalizedEmail == pending.NormalizedEmail, cancellationToken))
                {
                    dbContext.PendingCustomerRegistrations.Remove(pending);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return RegistrationCompletionStatus.AlreadyCompleted;
                }

                if (!await roleManager.RoleExistsAsync(RoleNames.Customer))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return RegistrationCompletionStatus.Unavailable;
                }

                var user = new ApplicationUser
                {
                    Id = pending.ReservedUserId,
                    UserName = pending.Email,
                    Email = pending.Email,
                    NormalizedEmail = pending.NormalizedEmail,
                    FirstName = pending.FirstName,
                    LastName = pending.LastName,
                    PasswordHash = pending.PasswordHash,
                    EmailConfirmed = true,
                    CreatedAtUtc = timeProvider.GetUtcNow(),
                    IsActive = true,
                };
                var creation = await userManager.CreateAsync(user);
                if (!creation.Succeeded)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return RegistrationCompletionStatus.Unavailable;
                }

                var roleAssignment = await userManager.AddToRoleAsync(user, RoleNames.Customer);
                if (!roleAssignment.Succeeded)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return RegistrationCompletionStatus.Unavailable;
                }

                dbContext.PendingCustomerRegistrations.Remove(pending);
                dbContext.AuditLogs.Add(CreateAudit(
                    "CustomerRegistrationCompleted",
                    "Customer registration completed after email ownership confirmation.",
                    user.Id,
                    context,
                    timeProvider.GetUtcNow()));
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return RegistrationCompletionStatus.Completed;
            });
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            return RegistrationCompletionStatus.Unavailable;
        }
    }

    internal static byte[] HashToken(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

    private static string CreateToken() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

    private static bool IsValid(PendingCustomerRegistration? pending, string token, DateTimeOffset now)
    {
        if (pending is null || string.IsNullOrWhiteSpace(token) || pending.TokenExpiresAtUtc < now)
        {
            return false;
        }

        var presentedHash = HashToken(token);
        return presentedHash.Length == pending.VerificationTokenHash.Length &&
               CryptographicOperations.FixedTimeEquals(presentedHash, pending.VerificationTokenHash);
    }

    private static ApplicationUser CreateCandidate(
        Guid id,
        BeginCustomerRegistration request,
        string email,
        DateTimeOffset now) => new()
        {
            Id = id,
            UserName = email,
            Email = email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            CreatedAtUtc = now,
            IsActive = true,
        };

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0)
        {
            return "***";
        }

        return $"{email[0]}***{email[at..]}";
    }

    internal static AuditLog CreateAudit(
        string action,
        string description,
        Guid? actorUserId,
        SecurityEventContext context,
        DateTimeOffset now) => new()
        {
            AdminUserId = actorUserId,
            ActionType = action,
            EntityType = "ApplicationUser",
            EntityId = actorUserId?.ToString(),
            Description = description,
            IpAddress = Truncate(context.IpAddress, 64),
            UserAgent = Truncate(context.UserAgent, 512),
            Route = Truncate(context.Route, 256),
            CorrelationId = Truncate(context.CorrelationId, 128) ?? Guid.NewGuid().ToString("N"),
            CreatedAtUtc = now,
        };

    private static string? Truncate(string? value, int maximumLength) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Length <= maximumLength ? value : value[..maximumLength];
}

public sealed class CustomerPasswordResetService(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider) : ICustomerPasswordResetService
{
    public async Task<bool> ResetAsync(
        string email,
        string token,
        string newPassword,
        SecurityEventContext context,
        CancellationToken cancellationToken = default)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            var user = await userManager.FindByEmailAsync(email.Trim());
            if (user is null || !user.IsActive || !user.EmailConfirmed)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            var result = await userManager.ResetPasswordAsync(user, token, newPassword);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return false;
            }

            dbContext.AuditLogs.Add(CustomerRegistrationService.CreateAudit(
                "CustomerPasswordReset",
                "Customer password reset completed using a valid single-use token.",
                user.Id,
                context,
                timeProvider.GetUtcNow()));
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        });
    }
}

public sealed class PendingRegistrationCleanupWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<SecurityOptions> securityOptions,
    IHostEnvironment environment,
    TimeProvider timeProvider,
    ILogger<PendingRegistrationCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing"))
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromHours(12), timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Expired pending customer registrations could not be cleaned.");
            }
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var cutoff = timeProvider.GetUtcNow().AddDays(-securityOptions.Value.PendingRegistrationRetentionDays);
        await dbContext.PendingCustomerRegistrations
            .Where(x => x.CreatedAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
