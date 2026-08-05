using System.Text.Json;
using AETKAHVE.Application.Notifications;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Infrastructure.Commerce;
using AETKAHVE.Infrastructure.Options;
using AETKAHVE.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Infrastructure.Notifications;

public sealed class OutboxIdentityMessageSender(
    AppDbContext dbContext,
    TimeProvider timeProvider,
    IOptions<NotificationOptions> notificationOptions,
    IDataProtectionProvider dataProtectionProvider) : IIdentityMessageSender
{
    public const string ProtectedTemplateKey = "IdentityProtected";
    public const string DataProtectionPurpose = "AETKAHVE.IdentityOutbox.v1";

    public async Task SendAsync(IdentityMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!notificationOptions.Value.EmailDeliveryEnabled)
        {
            throw new InvalidOperationException("Identity email delivery is disabled.");
        }
        if (string.IsNullOrWhiteSpace(message.Destination))
        {
            throw new ArgumentException("Identity message destination is required.", nameof(message));
        }

        var destination = message.Destination.Trim();
        var normalizedDestination = destination.ToUpperInvariant();
        var userId = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.NormalizedEmail == normalizedDestination)
            .Select(user => (Guid?)user.Id)
            .SingleOrDefaultAsync(cancellationToken);
        userId ??= await dbContext.PendingCustomerRegistrations
            .AsNoTracking()
            .Where(registration => registration.NormalizedEmail == normalizedDestination)
            .Select(registration => (Guid?)registration.ReservedUserId)
            .SingleOrDefaultAsync(cancellationToken);
        if (userId is null)
        {
            throw new InvalidOperationException("The Identity message destination does not belong to a persisted user or pending registration.");
        }

        var now = timeProvider.GetUtcNow();
        dbContext.NotificationDeliveries.Add(new NotificationDelivery
        {
            UserId = userId.Value,
            Channel = NotificationChannel.Email,
            Destination = destination,
            TemplateKey = ProtectedTemplateKey,
            PayloadJson = dataProtectionProvider
                .CreateProtector(DataProtectionPurpose)
                .Protect(JsonSerializer.Serialize(new DeliveryPayload(message.Subject, message.HtmlBody))),
            NextAttemptAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
