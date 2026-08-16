using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Text.Encodings.Web;
using System.Text.Json;
using AETKAHVE.Application.Security;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Infrastructure.Commerce;
using AETKAHVE.Infrastructure.Notifications;
using AETKAHVE.Infrastructure.Options;
using AETKAHVE.Infrastructure.Persistence;
using AETKAHVE.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Infrastructure.Identity;

public sealed class AdminAccountManagementService(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    ManagementSessionService managementSessions,
    IDataProtectionProvider dataProtectionProvider,
    IOptions<NotificationOptions> notificationOptions,
    TimeProvider timeProvider) : IAdminAccountManagementService
{
    private const string ResetPasswordPurpose = "ResetPassword";
    private readonly NotificationOptions _notificationOptions = notificationOptions.Value;

    public async Task<AdminAccountPage> SearchAsync(
        AdminAccountQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var now = timeProvider.GetUtcNow();
        var adminRoleId = await FindRoleIdAsync(RoleNames.Admin, cancellationToken);
        var superAdminRoleId = await FindRoleIdAsync(RoleNames.SuperAdmin, cancellationToken);
        if (adminRoleId is null)
        {
            return new AdminAccountPage([], page, pageSize, 0);
        }

        var source = dbContext.Users
            .AsNoTracking()
            .Where(user => user.DeletedAtUtc == null)
            .Where(user => dbContext.UserRoles.Any(userRole =>
                userRole.UserId == user.Id && userRole.RoleId == adminRoleId.Value));
        if (superAdminRoleId.HasValue)
        {
            source = source.Where(user => !dbContext.UserRoles.Any(userRole =>
                userRole.UserId == user.Id && userRole.RoleId == superAdminRoleId.Value));
        }

        var search = query.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.ToUpperInvariant();
            source = source.Where(user =>
                (user.NormalizedEmail != null && user.NormalizedEmail.Contains(normalized))
                || user.FirstName.ToUpper().Contains(normalized)
                || user.LastName.ToUpper().Contains(normalized));
        }

        var status = Enum.IsDefined(query.Status) ? query.Status : AdminAccountStatusFilter.All;
        source = status switch
        {
            AdminAccountStatusFilter.Active => source.Where(user =>
                user.IsActive && user.PasswordHash != null && user.EmailConfirmed),
            AdminAccountStatusFilter.Inactive => source.Where(user =>
                !user.IsActive && user.PasswordHash != null),
            AdminAccountStatusFilter.PendingInvitation => source.Where(user => user.PasswordHash == null),
            AdminAccountStatusFilter.Locked => source.Where(user =>
                user.LockoutEnd.HasValue && user.LockoutEnd.Value > now),
            _ => source,
        };

        var totalCount = await source.CountAsync(cancellationToken);
        var items = await source
            .OrderByDescending(user => user.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new AdminAccountSummary(
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email ?? string.Empty,
                user.IsActive,
                user.PasswordHash == null,
                user.LockoutEnd.HasValue && user.LockoutEnd.Value > now,
                user.LockoutEnd,
                user.CreatedAtUtc,
                user.LastLoginAtUtc))
            .ToListAsync(cancellationToken);

        return new AdminAccountPage(items, page, pageSize, totalCount);
    }

    public async Task<AdminAccountDetails?> GetAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await FindManagedAdminAsync(userId);
        if (user is null)
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        return new AdminAccountDetails(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email ?? string.Empty,
            user.IsActive,
            user.PasswordHash == null,
            user.LockoutEnd.HasValue && user.LockoutEnd.Value > now,
            user.LockoutEnd,
            user.CreatedAtUtc,
            user.LastLoginAtUtc);
    }

    public async Task<AdminAccountStartResult> CreateAsync(
        Guid actorUserId,
        CreateAdminAccount input,
        SecurityEventContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await IsAvailableSuperAdminAsync(actorUserId))
        {
            return FailureStart("Bu işlem için aktif SuperAdmin yetkisi gereklidir.");
        }

        var validation = ValidateIdentityInput(input.FirstName, input.LastName, input.Email);
        if (validation is not null)
        {
            return FailureStart(validation);
        }

        if (!await roleManager.RoleExistsAsync(RoleNames.Admin))
        {
            return FailureStart("Admin rolü sistemde hazır değil.");
        }

        var email = input.Email.Trim();
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return FailureStart("Bu e-posta adresi başka bir hesap tarafından kullanılıyor.");
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = false,
            FirstName = input.FirstName.Trim(),
            LastName = input.LastName.Trim(),
            CreatedAtUtc = timeProvider.GetUtcNow(),
            IsActive = false,
            LockoutEnabled = true,
        };

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var createResult = await userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return FailureStart(FirstIdentityError(createResult));
            }

            var roleResult = await userManager.AddToRoleAsync(user, RoleNames.Admin);
            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return FailureStart(FirstIdentityError(roleResult));
            }

            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            AddAudit(
                actorUserId,
                user.Id,
                "AdminAccountCreated",
                "An Admin account was created in pending-invitation state.",
                context,
                new { user.FirstName, user.LastName, Email = email, Role = RoleNames.Admin, user.IsActive });
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new AdminAccountStartResult(
                AdminAccountOperationResult.Success("Admin hesabı oluşturuldu ve davet gönderilmeye hazır."),
                new AdminAccountTokenDispatch(user.Id, email, token, AdminAccountTokenPurpose.Invitation));
        });
    }

    public async Task<AdminAccountUpdateResult> UpdateAsync(
        Guid actorUserId,
        Guid userId,
        UpdateAdminAccount input,
        SecurityEventContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await IsAvailableSuperAdminAsync(actorUserId))
        {
            return FailureUpdate("Bu işlem için aktif SuperAdmin yetkisi gereklidir.");
        }

        var validation = ValidateIdentityInput(input.FirstName, input.LastName, input.Email);
        if (validation is not null)
        {
            return FailureUpdate(validation);
        }

        var user = await FindManagedAdminAsync(userId);
        if (user is null)
        {
            return FailureUpdate("Admin hesabı bulunamadı.");
        }

        var newEmail = input.Email.Trim();
        var emailChanged = !string.Equals(user.Email, newEmail, StringComparison.OrdinalIgnoreCase);
        if (emailChanged)
        {
            if (!_notificationOptions.EmailDeliveryEnabled)
            {
                return FailureUpdate("E-posta teslimatı şu anda kullanılamıyor.");
            }

            var existing = await userManager.FindByEmailAsync(newEmail);
            if (existing is not null && existing.Id != user.Id)
            {
                return FailureUpdate("Bu e-posta adresi başka bir hesap tarafından kullanılıyor.");
            }
        }

        var oldValues = new { user.FirstName, user.LastName, user.Email };
        user.FirstName = input.FirstName.Trim();
        user.LastName = input.LastName.Trim();
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return FailureUpdate(FirstIdentityError(updateResult));
        }

        AdminEmailChangeDispatch? dispatch = null;
        if (emailChanged)
        {
            var token = await userManager.GenerateChangeEmailTokenAsync(user, newEmail);
            dispatch = new AdminEmailChangeDispatch(user.Id, newEmail, token);
        }

        AddAudit(
            actorUserId,
            user.Id,
            "AdminAccountUpdated",
            emailChanged
                ? "An Admin profile was updated and an email change was requested."
                : "An Admin profile was updated.",
            context,
            new { user.FirstName, user.LastName, RequestedEmail = emailChanged ? newEmail : user.Email },
            oldValues);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new AdminAccountUpdateResult(
            AdminAccountOperationResult.Success(emailChanged
                ? "Admin bilgileri güncellendi. Yeni e-posta adresine doğrulama bağlantısı gönderilecek."
                : "Admin bilgileri güncellendi."),
            dispatch);
    }

    public async Task<AdminAccountStartResult> ResendInvitationAsync(
        Guid actorUserId,
        Guid userId,
        SecurityEventContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await IsAvailableSuperAdminAsync(actorUserId))
        {
            return FailureStart("Bu işlem için aktif SuperAdmin yetkisi gereklidir.");
        }

        var user = await FindManagedAdminAsync(userId);
        if (user is null)
        {
            return FailureStart("Admin hesabı bulunamadı.");
        }

        if (user.PasswordHash is not null)
        {
            return FailureStart("Bu Admin davetini zaten tamamlamış.");
        }

        var stampResult = await userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
        {
            return FailureStart(FirstIdentityError(stampResult));
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        AddAudit(
            actorUserId,
            user.Id,
            "AdminInvitationRenewed",
            "An Admin invitation was renewed and previous invitation tokens were invalidated.",
            context);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new AdminAccountStartResult(
            AdminAccountOperationResult.Success("Yeni davet bağlantısı oluşturuldu."),
            new AdminAccountTokenDispatch(
                user.Id,
                user.Email ?? string.Empty,
                token,
                AdminAccountTokenPurpose.Invitation));
    }

    public async Task<AdminAccountStartResult> BeginPasswordResetAsync(
        Guid actorUserId,
        Guid userId,
        SecurityEventContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await IsAvailableSuperAdminAsync(actorUserId))
        {
            return FailureStart("Bu işlem için aktif SuperAdmin yetkisi gereklidir.");
        }

        var user = await FindManagedAdminAsync(userId);
        if (user is null)
        {
            return FailureStart("Admin hesabı bulunamadı.");
        }

        if (user.PasswordHash is null || !user.EmailConfirmed)
        {
            return FailureStart("Davetini tamamlamamış Admin için parola sıfırlama gönderilemez.");
        }

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        AddAudit(
            actorUserId,
            user.Id,
            "AdminPasswordResetRequested",
            "A password reset link was requested for an Admin account.",
            context);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new AdminAccountStartResult(
            AdminAccountOperationResult.Success("Parola sıfırlama bağlantısı oluşturuldu."),
            new AdminAccountTokenDispatch(
                user.Id,
                user.Email ?? string.Empty,
                token,
                AdminAccountTokenPurpose.PasswordReset));
    }

    public async Task<AdminAccountOperationResult> QueueTokenEmailAsync(
        AdminAccountTokenDispatch dispatch,
        string callbackUrl,
        CancellationToken cancellationToken = default)
    {
        if (!_notificationOptions.EmailDeliveryEnabled)
        {
            return AdminAccountOperationResult.Failure("E-posta teslimatı şu anda kullanılamıyor.");
        }

        var user = await FindManagedAdminAsync(dispatch.UserId);
        if (user is null || !string.Equals(user.Email, dispatch.Email, StringComparison.OrdinalIgnoreCase))
        {
            return AdminAccountOperationResult.Failure("Admin hesabı veya e-posta adresi artık geçerli değil.");
        }

        var encodedUrl = HtmlEncoder.Default.Encode(callbackUrl);
        var subject = dispatch.Purpose == AdminAccountTokenPurpose.Invitation
            ? "Admin davetinizi tamamlayın"
            : "Admin parolanızı sıfırlayın";
        var body = dispatch.Purpose == AdminAccountTokenPurpose.Invitation
            ? $"<p>Admin hesabınızı etkinleştirip parolanızı belirlemek için <a href=\"{encodedUrl}\">bağlantıyı açın</a>. Bağlantı 24 saat geçerlidir.</p>"
            : $"<p>Admin parolanızı sıfırlamak için <a href=\"{encodedUrl}\">bağlantıyı açın</a>. Bağlantı 24 saat geçerlidir.</p>";
        await QueueProtectedEmailAsync(user.Id, dispatch.Email, subject, body, cancellationToken);
        return AdminAccountOperationResult.Success("E-posta gönderim kuyruğuna alındı.");
    }

    public async Task<AdminAccountOperationResult> QueueEmailChangeAsync(
        AdminEmailChangeDispatch dispatch,
        string callbackUrl,
        CancellationToken cancellationToken = default)
    {
        if (!_notificationOptions.EmailDeliveryEnabled)
        {
            return AdminAccountOperationResult.Failure("E-posta teslimatı şu anda kullanılamıyor.");
        }

        var user = await FindManagedAdminAsync(dispatch.UserId);
        if (user is null)
        {
            return AdminAccountOperationResult.Failure("Admin hesabı artık geçerli değil.");
        }

        await QueueProtectedEmailAsync(
            user.Id,
            dispatch.NewEmail,
            "Yeni Admin e-posta adresinizi doğrulayın",
            $"<p>Admin hesabının e-posta değişikliğini tamamlamak için <a href=\"{HtmlEncoder.Default.Encode(callbackUrl)}\">bağlantıyı açın</a>.</p>",
            cancellationToken);
        return AdminAccountOperationResult.Success("E-posta doğrulama bağlantısı gönderim kuyruğuna alındı.");
    }

    public async Task<AdminAccountTokenValidation> ValidateTokenAsync(
        Guid userId,
        string token,
        AdminAccountTokenPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await FindManagedAdminAsync(userId);
        if (user is null || string.IsNullOrWhiteSpace(token))
        {
            return new AdminAccountTokenValidation(false, string.Empty, false);
        }

        var correctState = purpose == AdminAccountTokenPurpose.Invitation
            ? user.PasswordHash is null
            : user.PasswordHash is not null && user.EmailConfirmed;
        var valid = correctState && await userManager.VerifyUserTokenAsync(
            user,
            userManager.Options.Tokens.PasswordResetTokenProvider,
            ResetPasswordPurpose,
            token);
        return new AdminAccountTokenValidation(
            valid,
            MaskEmail(user.Email),
            user.IsActive);
    }

    public async Task<AdminAccountOperationResult> CompleteInvitationAsync(
        Guid userId,
        string token,
        string password,
        SecurityEventContext context,
        CancellationToken cancellationToken = default)
    {
        var user = await FindManagedAdminAsync(userId);
        if (user is null || user.PasswordHash is not null)
        {
            return AdminAccountOperationResult.Failure("Davet bağlantısı geçersiz, kullanılmış veya süresi dolmuş.");
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var passwordResult = await userManager.ResetPasswordAsync(user, token, password);
            if (!passwordResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return AdminAccountOperationResult.Failure(
                    IsTokenError(passwordResult)
                        ? "Davet bağlantısı geçersiz, kullanılmış veya süresi dolmuş."
                        : FirstIdentityError(passwordResult));
            }

            user.EmailConfirmed = true;
            user.IsActive = true;
            user.DeletedAtUtc = null;
            user.LockoutEnabled = true;
            user.LockoutEnd = null;
            user.AccessFailedCount = 0;
            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return AdminAccountOperationResult.Failure(FirstIdentityError(updateResult));
            }

            AddAudit(
                user.Id,
                user.Id,
                "AdminInvitationCompleted",
                "An Admin completed the invitation and activated the account.",
                context);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return AdminAccountOperationResult.Success("Admin hesabınız etkinleştirildi. Parolanızla giriş yapabilirsiniz.");
        });
    }

    public async Task<AdminAccountOperationResult> CompletePasswordResetAsync(
        Guid userId,
        string token,
        string password,
        SecurityEventContext context,
        CancellationToken cancellationToken = default)
    {
        var user = await FindManagedAdminAsync(userId);
        if (user is null || user.PasswordHash is null || !user.EmailConfirmed)
        {
            return AdminAccountOperationResult.Failure("Parola sıfırlama bağlantısı geçersiz veya süresi dolmuş.");
        }

        var resetResult = await userManager.ResetPasswordAsync(user, token, password);
        if (!resetResult.Succeeded)
        {
            return AdminAccountOperationResult.Failure(
                IsTokenError(resetResult)
                    ? "Parola sıfırlama bağlantısı geçersiz veya süresi dolmuş."
                    : FirstIdentityError(resetResult));
        }

        await managementSessions.RevokeAllAsync(user.Id, "PasswordReset", cancellationToken);
        AddAudit(
            user.Id,
            user.Id,
            "AdminPasswordResetCompleted",
            "An Admin password reset was completed using a valid single-use token.",
            context);
        await dbContext.SaveChangesAsync(cancellationToken);
        return AdminAccountOperationResult.Success("Parolanız değiştirildi. Yeni parolanızla giriş yapabilirsiniz.");
    }

    public async Task<AdminAccountTokenValidation> ValidateEmailChangeAsync(
        Guid userId,
        string newEmail,
        string token,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await FindManagedAdminAsync(userId);
        if (user is null || string.IsNullOrWhiteSpace(newEmail) || string.IsNullOrWhiteSpace(token))
        {
            return new AdminAccountTokenValidation(false, MaskEmail(newEmail), false);
        }

        var valid = await userManager.VerifyUserTokenAsync(
            user,
            userManager.Options.Tokens.ChangeEmailTokenProvider,
            $"ChangeEmail:{newEmail}",
            token);
        return new AdminAccountTokenValidation(valid, MaskEmail(newEmail), user.IsActive);
    }

    public async Task<AdminEmailChangeCompletionResult> ConfirmEmailChangeAsync(
        Guid userId,
        string newEmail,
        string token,
        SecurityEventContext context,
        CancellationToken cancellationToken = default)
    {
        var user = await FindManagedAdminAsync(userId);
        if (user is null || !IsValidEmail(newEmail))
        {
            return FailureEmailChange("E-posta doğrulama bağlantısı geçersiz veya süresi dolmuş.");
        }

        var email = newEmail.Trim();
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null && existing.Id != user.Id)
        {
            return FailureEmailChange("Bu e-posta adresi başka bir hesap tarafından kullanılıyor.");
        }

        var oldEmail = user.Email ?? string.Empty;
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            var emailResult = await userManager.ChangeEmailAsync(user, email, token);
            if (!emailResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return FailureEmailChange("E-posta doğrulama bağlantısı geçersiz, kullanılmış veya süresi dolmuş.");
            }

            var userNameResult = await userManager.SetUserNameAsync(user, email);
            if (!userNameResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return FailureEmailChange(FirstIdentityError(userNameResult));
            }

            var stampResult = await userManager.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return FailureEmailChange(FirstIdentityError(stampResult));
            }

            await managementSessions.RevokeAllAsync(user.Id, "EmailChanged", cancellationToken);
            AddAudit(
                user.Id,
                user.Id,
                "AdminEmailChanged",
                "An Admin email address change was confirmed.",
                context,
                new { Email = email },
                new { Email = oldEmail });

            AdminAccountTokenDispatch? invitationDispatch = null;
            if (user.PasswordHash is null)
            {
                var invitationToken = await userManager.GeneratePasswordResetTokenAsync(user);
                invitationDispatch = new AdminAccountTokenDispatch(
                    user.Id,
                    email,
                    invitationToken,
                    AdminAccountTokenPurpose.Invitation);
            }

            if (!string.IsNullOrWhiteSpace(oldEmail))
            {
                await QueueProtectedEmailAsync(
                    user.Id,
                    oldEmail,
                    "Admin hesabınızın e-posta adresi değiştirildi",
                    "<p>Admin hesabınızın e-posta adresi değiştirildi. Bu işlemi siz başlatmadıysanız SuperAdmin ile iletişime geçin.</p>",
                    cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new AdminEmailChangeCompletionResult(
                AdminAccountOperationResult.Success("E-posta adresiniz doğrulandı ve güncellendi."),
                invitationDispatch);
        });
    }

    public async Task<AdminAccountOperationResult> SetActiveAsync(
        Guid actorUserId,
        Guid userId,
        bool isActive,
        SecurityEventContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await IsAvailableSuperAdminAsync(actorUserId))
        {
            return AdminAccountOperationResult.Failure("Bu işlem için aktif SuperAdmin yetkisi gereklidir.");
        }

        var user = await FindManagedAdminAsync(userId);
        if (user is null)
        {
            return AdminAccountOperationResult.Failure("Admin hesabı bulunamadı.");
        }

        if (isActive && (user.PasswordHash is null || !user.EmailConfirmed))
        {
            return AdminAccountOperationResult.Failure("Davetini tamamlamamış Admin aktifleştirilemez.");
        }

        if (user.IsActive == isActive)
        {
            return AdminAccountOperationResult.Success(isActive ? "Admin zaten aktif." : "Admin zaten pasif.");
        }

        user.IsActive = isActive;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return AdminAccountOperationResult.Failure(FirstIdentityError(updateResult));
        }

        if (!isActive)
        {
            var stampResult = await userManager.UpdateSecurityStampAsync(user);
            if (!stampResult.Succeeded)
            {
                return AdminAccountOperationResult.Failure(FirstIdentityError(stampResult));
            }

            await managementSessions.RevokeAllAsync(user.Id, "AdminDeactivated", cancellationToken);
        }

        AddAudit(
            actorUserId,
            user.Id,
            isActive ? "AdminAccountActivated" : "AdminAccountDeactivated",
            isActive ? "An Admin account was activated." : "An Admin account was deactivated and its sessions were revoked.",
            context,
            new { IsActive = isActive },
            new { IsActive = !isActive });
        await dbContext.SaveChangesAsync(cancellationToken);
        return AdminAccountOperationResult.Success(isActive ? "Admin aktifleştirildi." : "Admin pasifleştirildi ve oturumları kapatıldı.");
    }

    public async Task<AdminAccountOperationResult> UnlockAsync(
        Guid actorUserId,
        Guid userId,
        SecurityEventContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await IsAvailableSuperAdminAsync(actorUserId))
        {
            return AdminAccountOperationResult.Failure("Bu işlem için aktif SuperAdmin yetkisi gereklidir.");
        }

        var user = await FindManagedAdminAsync(userId);
        if (user is null)
        {
            return AdminAccountOperationResult.Failure("Admin hesabı bulunamadı.");
        }

        var lockoutResult = await userManager.SetLockoutEndDateAsync(user, null);
        if (!lockoutResult.Succeeded)
        {
            return AdminAccountOperationResult.Failure(FirstIdentityError(lockoutResult));
        }

        var resetResult = await userManager.ResetAccessFailedCountAsync(user);
        if (!resetResult.Succeeded)
        {
            return AdminAccountOperationResult.Failure(FirstIdentityError(resetResult));
        }

        AddAudit(
            actorUserId,
            user.Id,
            "AdminAccountUnlocked",
            "An Admin account lockout was cleared.",
            context);
        await dbContext.SaveChangesAsync(cancellationToken);
        return AdminAccountOperationResult.Success("Admin hesap kilidi kaldırıldı.");
    }

    public async Task<AdminAccountOperationResult> DeleteAsync(
        Guid actorUserId,
        Guid userId,
        SecurityEventContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await IsAvailableSuperAdminAsync(actorUserId))
        {
            return AdminAccountOperationResult.Failure("Bu işlem için aktif SuperAdmin yetkisi gereklidir.");
        }

        var user = await FindManagedAdminAsync(userId);
        if (user is null)
        {
            return AdminAccountOperationResult.Failure("Admin hesabı bulunamadı.");
        }

        var snapshot = new
        {
            user.FirstName,
            user.LastName,
            user.Email,
            Role = RoleNames.Admin,
            user.IsActive,
            user.CreatedAtUtc,
            user.LastLoginAtUtc,
        };
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            await dbContext.NotificationDeliveries
                .Where(delivery => delivery.UserId == user.Id
                    && delivery.TemplateKey == OutboxIdentityMessageSender.ProtectedTemplateKey)
                .ExecuteDeleteAsync(cancellationToken);

            AddAudit(
                actorUserId,
                user.Id,
                "AdminAccountPermanentlyDeleted",
                "An Admin account and its Identity data were permanently deleted.",
                context,
                oldValues: snapshot);
            await dbContext.SaveChangesAsync(cancellationToken);

            var deleteResult = await userManager.DeleteAsync(user);
            if (!deleteResult.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);
                return AdminAccountOperationResult.Failure(FirstIdentityError(deleteResult));
            }

            await transaction.CommitAsync(cancellationToken);
            return AdminAccountOperationResult.Success("Admin hesabı kalıcı olarak silindi.");
        });
    }

    private async Task<Guid?> FindRoleIdAsync(string roleName, CancellationToken cancellationToken) =>
        await dbContext.Roles
            .AsNoTracking()
            .Where(role => role.NormalizedName == roleName.ToUpperInvariant())
            .Select(role => (Guid?)role.Id)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<ApplicationUser?> FindManagedAdminAsync(Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.DeletedAtUtc.HasValue)
        {
            return null;
        }

        var roles = await userManager.GetRolesAsync(user);
        return roles.Contains(RoleNames.Admin, StringComparer.OrdinalIgnoreCase)
            && !roles.Contains(RoleNames.SuperAdmin, StringComparer.OrdinalIgnoreCase)
                ? user
                : null;
    }

    private async Task<bool> IsAvailableSuperAdminAsync(Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is { IsActive: true, DeletedAtUtc: null }
            && await userManager.IsInRoleAsync(user, RoleNames.SuperAdmin);
    }

    private async Task QueueProtectedEmailAsync(
        Guid userId,
        string destination,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        dbContext.NotificationDeliveries.Add(new NotificationDelivery
        {
            UserId = userId,
            Channel = NotificationChannel.Email,
            Destination = destination,
            TemplateKey = OutboxIdentityMessageSender.ProtectedTemplateKey,
            PayloadJson = dataProtectionProvider
                .CreateProtector(OutboxIdentityMessageSender.DataProtectionPurpose)
                .Protect(JsonSerializer.Serialize(new DeliveryPayload(subject, body))),
            NextAttemptAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void AddAudit(
        Guid actorUserId,
        Guid targetUserId,
        string action,
        string description,
        SecurityEventContext context,
        object? newValues = null,
        object? oldValues = null)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            AdminUserId = actorUserId,
            ActionType = action,
            EntityType = nameof(ApplicationUser),
            EntityId = targetUserId.ToString("D"),
            OldValues = SerializeAuditValues(oldValues),
            NewValues = SerializeAuditValues(newValues),
            Description = description,
            IpAddress = Truncate(context.IpAddress, 64),
            UserAgent = Truncate(context.UserAgent, 512),
            Route = Truncate(context.Route, 256),
            CorrelationId = Truncate(context.CorrelationId, 128) ?? Guid.NewGuid().ToString("N"),
            CreatedAtUtc = timeProvider.GetUtcNow(),
        });
    }

    private static string? ValidateIdentityInput(string firstName, string lastName, string email)
    {
        if (string.IsNullOrWhiteSpace(firstName) || firstName.Trim().Length > 100)
        {
            return "Ad zorunludur ve en fazla 100 karakter olabilir.";
        }

        if (string.IsNullOrWhiteSpace(lastName) || lastName.Trim().Length > 100)
        {
            return "Soyad zorunludur ve en fazla 100 karakter olabilir.";
        }

        return IsValidEmail(email) ? null : "Geçerli bir e-posta adresi girin.";
    }

    private static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email)
        && email.Trim().Length <= 320
        && new EmailAddressAttribute().IsValid(email.Trim());

    private static string FirstIdentityError(IdentityResult result) =>
        result.Errors.FirstOrDefault()?.Description ?? "İşlem tamamlanamadı.";

    private static bool IsTokenError(IdentityResult result) =>
        result.Errors.Any(error => string.Equals(error.Code, "InvalidToken", StringComparison.OrdinalIgnoreCase));

    private static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return string.Empty;
        }

        var parts = email.Split('@', 2);
        return parts.Length == 2
            ? $"{(parts[0].Length > 0 ? parts[0][0] : '*')}***@{parts[1]}"
            : "***";
    }

    private static string? SerializeAuditValues(object? values)
    {
        if (values is null)
        {
            return null;
        }

        var json = JsonSerializer.Serialize(values);
        return json.Length <= 4000 ? json : json[..4000];
    }

    private static string? Truncate(string? value, int maximumLength) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= maximumLength ? value : value[..maximumLength];

    private static AdminAccountStartResult FailureStart(string message) =>
        new(AdminAccountOperationResult.Failure(message));

    private static AdminAccountUpdateResult FailureUpdate(string message) =>
        new(AdminAccountOperationResult.Failure(message));

    private static AdminEmailChangeCompletionResult FailureEmailChange(string message) =>
        new(AdminAccountOperationResult.Failure(message));
}
