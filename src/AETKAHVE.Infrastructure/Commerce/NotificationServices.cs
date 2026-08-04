using System.Text.Json;
using AETKAHVE.Application.Commerce;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Infrastructure.Options;
using AETKAHVE.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Infrastructure.Commerce;

public sealed class NotificationQueue(AppDbContext dbContext, TimeProvider timeProvider) : INotificationQueue
{
    public async Task EnqueueOrderAsync(Order order, string templateKey, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.AsNoTracking().Where(x => x.Id == order.UserId)
            .Select(x => new { x.Email, x.PhoneNumber }).SingleAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var notification = new Notification
        {
            UserId = order.UserId,
            Title = "Sipariş güncellemesi",
            Message = $"{order.OrderNumber} numaralı siparişinizin durumu: {order.Status}.",
            Type = templateKey,
            RelatedEntityType = nameof(Order),
            RelatedEntityId = order.Id,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        dbContext.Notifications.Add(notification);

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            dbContext.NotificationDeliveries.Add(CreateDelivery(notification, NotificationChannel.Email, user.Email,
                new DeliveryPayload("AETERNUM RECTUS LUCIS — Sipariş güncellemesi", notification.Message), templateKey, now));
        }
        if (!string.IsNullOrWhiteSpace(user.PhoneNumber))
        {
            dbContext.NotificationDeliveries.Add(CreateDelivery(notification, NotificationChannel.Sms, user.PhoneNumber,
                new DeliveryPayload(string.Empty, notification.Message), templateKey, now));
        }
    }

    private static NotificationDelivery CreateDelivery(Notification notification, NotificationChannel channel, string destination, DeliveryPayload payload, string template, DateTimeOffset now) =>
        new()
        {
            Notification = notification,
            UserId = notification.UserId,
            Channel = channel,
            Destination = destination,
            TemplateKey = template,
            PayloadJson = JsonSerializer.Serialize(payload),
            NextAttemptAtUtc = now,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
}

public sealed record DeliveryPayload(string Subject, string Body);

public sealed class NotificationDeliveryProcessor(
    AppDbContext dbContext,
    IEmailSender emailSender,
    ISmsSender smsSender,
    IOptions<NotificationOptions> options,
    TimeProvider timeProvider)
{
    private readonly NotificationOptions _options = options.Value;

    public async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var expiredLease = now.AddSeconds(-_options.ProcessingLeaseSeconds);
        var source = dbContext.NotificationDeliveries
            .Where(x => (x.Status == DeliveryStatus.Pending || x.Status == DeliveryStatus.Failed || x.Status == DeliveryStatus.Processing) && x.AttemptCount < _options.MaximumAttempts);
        List<NotificationDelivery> deliveries;
        if (dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            deliveries = (await source.ToListAsync(cancellationToken))
                .Where(x => x.Status == DeliveryStatus.Processing ? x.UpdatedAtUtc <= expiredLease : x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now)
                .OrderBy(x => x.CreatedAtUtc).Take(20).ToList();
        }
        else
        {
            deliveries = await source.Where(x => x.Status == DeliveryStatus.Processing ? x.UpdatedAtUtc <= expiredLease : x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= now)
                .OrderBy(x => x.CreatedAtUtc).Take(20).ToListAsync(cancellationToken);
        }

        foreach (var delivery in deliveries)
        {
            delivery.Status = DeliveryStatus.Processing;
            delivery.AttemptCount++;
            delivery.ConcurrencyToken = Guid.NewGuid();
            delivery.UpdatedAtUtc = now;
            await dbContext.SaveChangesAsync(cancellationToken);

            DeliveryResult result;
            try
            {
                var payload = JsonSerializer.Deserialize<DeliveryPayload>(delivery.PayloadJson) ?? new DeliveryPayload(string.Empty, string.Empty);
                result = delivery.Channel switch
                {
                    NotificationChannel.Email => await emailSender.SendAsync(new EmailMessage(delivery.Destination, payload.Subject, payload.Body), cancellationToken),
                    NotificationChannel.Sms => await smsSender.SendAsync(new SmsMessage(delivery.Destination, payload.Body), cancellationToken),
                    _ => new DeliveryResult(true, null, null),
                };
            }
            catch (Exception exception)
            {
                result = new DeliveryResult(false, null, exception.GetType().Name);
            }

            delivery.Status = result.Succeeded ? DeliveryStatus.Delivered : DeliveryStatus.Failed;
            delivery.DeliveredAtUtc = result.Succeeded ? timeProvider.GetUtcNow() : null;
            delivery.LastError = result.FailureReason is null ? null : result.FailureReason[..Math.Min(500, result.FailureReason.Length)];
            delivery.NextAttemptAtUtc = result.Succeeded ? null : timeProvider.GetUtcNow().AddMinutes(Math.Pow(2, delivery.AttemptCount));
            delivery.ConcurrencyToken = Guid.NewGuid();
            delivery.UpdatedAtUtc = timeProvider.GetUtcNow();
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

public sealed class NotificationDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<NotificationOptions> options,
    IHostEnvironment environment,
    TimeProvider timeProvider,
    ILogger<NotificationDeliveryWorker> logger) : BackgroundService
{
    private readonly NotificationOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (environment.IsEnvironment("Testing")) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollIntervalSeconds), timeProvider);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Notification delivery batch failed.");
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<NotificationDeliveryProcessor>().ProcessBatchAsync(cancellationToken);
    }
}
