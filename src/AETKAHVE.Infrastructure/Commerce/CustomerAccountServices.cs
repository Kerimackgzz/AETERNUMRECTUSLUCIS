using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using AETKAHVE.Application.Commerce;
using AETKAHVE.Domain.Common;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Infrastructure.Identity;
using AETKAHVE.Infrastructure.Notifications;
using AETKAHVE.Infrastructure.Options;
using AETKAHVE.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Infrastructure.Commerce;

public sealed class CustomerAccountQueryService(AppDbContext dbContext, ICartService cartService) : ICustomerAccountQueryService
{
    public async Task<CustomerAccountDashboard> GetDashboardAsync(Guid userId, CancellationToken cancellationToken)
    {
        var summary = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId && user.IsActive && user.DeletedAtUtc == null)
            .Select(user => new
            {
                Profile = new CustomerProfileDetails(
                    user.Id,
                    user.FirstName,
                    user.LastName,
                    user.Email ?? string.Empty,
                    user.PhoneNumber,
                    user.DateOfBirth,
                    user.CreatedAtUtc,
                    user.LastLoginAtUtc,
                    user.ProfileImageStorageKey != null),
                OrderCount = dbContext.Orders.Count(order => order.UserId == user.Id),
                FavoriteCount = dbContext.Favorites.Count(favorite => favorite.UserId == user.Id),
                AddressCount = dbContext.Addresses.Count(address => address.UserId == user.Id),
                UnreadNotificationCount = dbContext.Notifications.Count(notification => notification.UserId == user.Id && !notification.IsRead),
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Customer account is unavailable.");

        var latestOrderQuery = dbContext.Orders
            .AsNoTracking()
            .Where(order => order.UserId == userId)
            .Select(order => new CustomerLatestOrder(
                order.Id,
                order.OrderNumber,
                order.Status,
                order.GrandTotal,
                order.Currency,
                order.CreatedAtUtc));
        var latestOrder = dbContext.Database.IsSqlite()
            ? (await latestOrderQuery.ToListAsync(cancellationToken))
                .OrderByDescending(order => order.CreatedAtUtc)
                .FirstOrDefault()
            : await latestOrderQuery
                .OrderByDescending(order => order.CreatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

        var cart = await cartService.GetAsync(new CartOwner(userId, null), cancellationToken);
        var cartPreview = new CustomerCartPreview(
            cart.Items.Take(3).Select(item => new CustomerCartPreviewLine(
                item.ItemId,
                item.ProductName,
                item.VariantName,
                item.Quantity,
                item.LineTotal,
                item.ImageUrl)).ToList(),
            cart.ItemCount,
            cart.GrandTotal,
            cart.Currency);

        return new CustomerAccountDashboard(
            summary.Profile,
            new CustomerAccountCounters(
                summary.OrderCount,
                summary.FavoriteCount,
                summary.AddressCount,
                summary.UnreadNotificationCount),
            cartPreview,
            latestOrder);
    }
}

public sealed class CustomerProfileService(
    AppDbContext dbContext,
    UserManager<ApplicationUser> userManager,
    IFileStorageService fileStorage,
    IDataProtectionProvider dataProtectionProvider,
    IOptions<NotificationOptions> notificationOptions,
    TimeProvider timeProvider,
    ILogger<CustomerProfileService> logger) : ICustomerProfileService
{
    public const long MaximumProfilePhotoBytes = 2 * 1024 * 1024;
    private static readonly HashSet<string> ProfilePhotoContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/png", "image/webp",
    };

    public async Task<CustomerProfileDetails?> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId && user.IsActive && user.DeletedAtUtc == null)
            .Select(user => new CustomerProfileDetails(
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email ?? string.Empty,
                user.PhoneNumber,
                user.DateOfBirth,
                user.CreatedAtUtc,
                user.LastLoginAtUtc,
                user.ProfileImageStorageKey != null))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ServiceResult> UpdateAsync(Guid userId, CustomerProfileUpdate update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);
        var user = await FindAvailableUserAsync(userId);
        if (user is null) return ServiceResult.Failure("Hesap bulunamadı.");

        var firstName = update.FirstName.Trim();
        var lastName = update.LastName.Trim();
        var phone = string.IsNullOrWhiteSpace(update.PhoneNumber) ? null : update.PhoneNumber.Trim();
        if (firstName.Length is < 1 or > 100 || lastName.Length is < 1 or > 100 || phone?.Length > 30)
            return ServiceResult.Failure("Profil bilgileri doğrulanamadı.");
        if (update.DateOfBirth is { } birthDate && (birthDate > DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime) || birthDate.Year < 1900))
            return ServiceResult.Failure("Doğum tarihi doğrulanamadı.");

        user.FirstName = firstName;
        user.LastName = lastName;
        user.PhoneNumber = phone;
        user.DateOfBirth = update.DateOfBirth;
        var result = await userManager.UpdateAsync(user);
        return result.Succeeded
            ? ServiceResult.Success("Profil bilgileriniz güncellendi.")
            : ServiceResult.Failure(FirstIdentityError(result));
    }

    public async Task<ServiceResult> SavePhotoAsync(
        Guid userId,
        Stream content,
        long length,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        if (length is <= 0 or > MaximumProfilePhotoBytes)
            return ServiceResult.Failure("Profil fotoğrafı en fazla 2 MiB olabilir.");
        if (!ProfilePhotoContentTypes.Contains(contentType))
            return ServiceResult.Failure("Yalnız JPEG, PNG veya WebP fotoğraflar kabul edilir.");

        var user = await FindAvailableUserAsync(userId);
        if (user is null) return ServiceResult.Failure("Hesap bulunamadı.");

        StoredFile stored;
        try
        {
            stored = await fileStorage.SaveAsync(content, fileName, contentType, cancellationToken);
        }
        catch (CommerceRuleException exception)
        {
            return ServiceResult.Failure(exception.Message);
        }

        var previousKey = user.ProfileImageStorageKey;
        user.ProfileImageStorageKey = stored.StorageKey;
        var updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            await fileStorage.DeleteAsync(stored.StorageKey, cancellationToken);
            return ServiceResult.Failure(FirstIdentityError(updateResult));
        }

        if (!string.IsNullOrWhiteSpace(previousKey))
        {
            try
            {
                await fileStorage.DeleteAsync(previousKey, cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CommerceRuleException)
            {
                logger.LogWarning(exception, "The replaced customer profile image could not be deleted.");
            }
        }

        return ServiceResult.Success("Profil fotoğrafınız güncellendi.");
    }

    public async Task<CustomerProfilePhoto?> OpenPhotoAsync(Guid userId, CancellationToken cancellationToken)
    {
        var key = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId && user.IsActive && user.DeletedAtUtc == null)
            .Select(user => user.ProfileImageStorageKey)
            .SingleOrDefaultAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(key)) return null;
        var stream = await fileStorage.OpenReadAsync(key, cancellationToken);
        return stream is null ? null : new CustomerProfilePhoto(stream, ContentTypeFor(key));
    }

    public async Task<ServiceResult> DeletePhotoAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await FindAvailableUserAsync(userId);
        if (user is null) return ServiceResult.Failure("Hesap bulunamadı.");
        var previousKey = user.ProfileImageStorageKey;
        if (string.IsNullOrWhiteSpace(previousKey)) return ServiceResult.Success("Profil fotoğrafınız zaten kaldırılmış.");

        user.ProfileImageStorageKey = null;
        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded) return ServiceResult.Failure(FirstIdentityError(result));
        try
        {
            await fileStorage.DeleteAsync(previousKey, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CommerceRuleException)
        {
            logger.LogWarning(exception, "The deleted customer profile image file could not be removed.");
        }
        return ServiceResult.Success("Profil fotoğrafınız kaldırıldı.");
    }

    public async Task<CustomerEmailChangeStartResult> BeginEmailChangeAsync(
        Guid userId,
        string currentPassword,
        string newEmail,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await FindAvailableUserAsync(userId);
        if (user is null) return new(false, "Hesap bulunamadı.");
        if (!await userManager.CheckPasswordAsync(user, currentPassword))
            return new(false, "Mevcut parola doğrulanamadı.");

        var email = newEmail.Trim();
        var emailValidation = new EmailAddressAttribute();
        if (email.Length > 320 || !emailValidation.IsValid(email))
            return new(false, "Yeni e-posta adresi geçerli değil.");
        if (string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase))
            return new(false, "Yeni e-posta adresi mevcut adresinizden farklı olmalıdır.");
        if (await userManager.FindByEmailAsync(email) is not null)
            return new(false, "Bu e-posta adresi başka bir hesap tarafından kullanılıyor.");
        if (!notificationOptions.Value.EmailDeliveryEnabled)
            return new(false, "E-posta teslimatı şu anda kullanılamıyor.");

        var token = await userManager.GenerateChangeEmailTokenAsync(user, email);
        return new(true, "Doğrulama bağlantısı yeni e-posta adresinize gönderildi.", token);
    }

    public Task QueueEmailChangeConfirmationAsync(
        Guid userId,
        string newEmail,
        string confirmationUrl,
        CancellationToken cancellationToken) =>
        QueueProtectedEmailAsync(
            userId,
            newEmail.Trim(),
            "Yeni e-posta adresinizi doğrulayın",
            $"<p>E-posta adresi değişikliğini tamamlamak için <a href=\"{System.Text.Encodings.Web.HtmlEncoder.Default.Encode(confirmationUrl)}\">bağlantıyı açın</a>.</p>",
            cancellationToken);

    public async Task<CustomerEmailChangeValidation> ValidateEmailChangeAsync(
        Guid userId,
        string newEmail,
        string token,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await FindAvailableUserAsync(userId);
        if (user is null || string.IsNullOrWhiteSpace(newEmail) || string.IsNullOrWhiteSpace(token))
            return new(false, MaskEmail(newEmail));
        var valid = await userManager.VerifyUserTokenAsync(
            user,
            userManager.Options.Tokens.ChangeEmailTokenProvider,
            $"ChangeEmail:{newEmail}",
            token);
        return new(valid, MaskEmail(newEmail));
    }

    public async Task<ServiceResult> ConfirmEmailChangeAsync(
        Guid userId,
        string newEmail,
        string token,
        CancellationToken cancellationToken)
    {
        var user = await FindAvailableUserAsync(userId);
        if (user is null) return ServiceResult.Failure("Hesap bulunamadı.");
        var oldEmail = user.Email;
        if (string.IsNullOrWhiteSpace(oldEmail)) return ServiceResult.Failure("Mevcut e-posta adresi bulunamadı.");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var emailResult = await userManager.ChangeEmailAsync(user, newEmail.Trim(), token);
        if (!emailResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult.Failure("Doğrulama bağlantısı geçersiz, kullanılmış veya süresi dolmuş.");
        }

        var userNameResult = await userManager.SetUserNameAsync(user, newEmail.Trim());
        if (!userNameResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult.Failure(FirstIdentityError(userNameResult));
        }

        var stampResult = await userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult.Failure(FirstIdentityError(stampResult));
        }

        await QueueProtectedEmailAsync(
            user.Id,
            oldEmail,
            "Hesabınızın e-posta adresi değiştirildi",
            "<p>AETERNUM hesabınızın e-posta adresi değiştirildi. Bu işlemi siz yapmadıysanız destek ekibimizle iletişime geçin.</p>",
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ServiceResult.Success("E-posta adresiniz değiştirildi. Lütfen yeniden giriş yapın.");
    }

    public async Task<ServiceResult> ChangePasswordAsync(
        Guid userId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await FindAvailableUserAsync(userId);
        if (user is null) return ServiceResult.Failure("Hesap bulunamadı.");
        var result = await userManager.ChangePasswordAsync(user, currentPassword, newPassword);
        return result.Succeeded
            ? ServiceResult.Success("Parolanız değiştirildi. Lütfen yeniden giriş yapın.")
            : ServiceResult.Failure(FirstIdentityError(result));
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

    private async Task<ApplicationUser?> FindAvailableUserAsync(Guid userId)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is { IsActive: true, DeletedAtUtc: null } ? user : null;
    }

    private static string FirstIdentityError(IdentityResult result) =>
        result.Errors.FirstOrDefault()?.Description ?? "İşlem tamamlanamadı.";

    private static string ContentTypeFor(string key) => Path.GetExtension(key).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "application/octet-stream",
    };

    private static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return string.Empty;
        var parts = email.Split('@', 2);
        if (parts.Length != 2) return "***";
        var visible = parts[0].Length > 0 ? parts[0][0].ToString() : string.Empty;
        return $"{visible}***@{parts[1]}";
    }
}
