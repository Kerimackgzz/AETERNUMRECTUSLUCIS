using System.Text.Json;
using AETKAHVE.Application.Commerce;
using AETKAHVE.Application.Notifications;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Infrastructure.Commerce;
using AETKAHVE.Infrastructure.DependencyInjection;
using AETKAHVE.Infrastructure.Identity;
using AETKAHVE.Infrastructure.Notifications;
using AETKAHVE.Infrastructure.Options;
using AETKAHVE.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AETKAHVE.UnitTests;

public sealed class ProductionReadinessTests
{
    [Fact]
    public void Data_protection_payload_survives_a_service_provider_restart()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"aetkahve-dp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(contentRoot);
        try
        {
            var configuration = CreateConfiguration(
                useMockProviders: true,
                keyRingPath: Path.Combine(contentRoot, "keys"));
            var protectedPayload = Protect(configuration, contentRoot, "persistent payload");

            var unprotectedPayload = Unprotect(configuration, contentRoot, protectedPayload);

            Assert.Equal("persistent payload", unprotectedPayload);
            Assert.NotEmpty(Directory.EnumerateFiles(Path.Combine(contentRoot, "keys"), "*.xml"));
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public void Production_rejects_mock_notification_providers()
    {
        var services = CreateCommerceServices(
            Environments.Production,
            CreateConfiguration(useMockProviders: true));
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<NotificationOptions>>().Value);

        Assert.Contains("UseMockProviders must be false", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_rejects_incomplete_smtp_configuration()
    {
        var services = CreateCommerceServices(
            Environments.Production,
            CreateConfiguration(useMockProviders: false));
        using var provider = services.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<SmtpOptions>>().Value);

        Assert.Contains("Smtp:Host is required", exception.Message, StringComparison.Ordinal);
        Assert.Contains("deliverable address", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_fails_closed_until_real_payment_and_shipping_adapters_are_registered()
    {
        var services = CreateCommerceServices(
            Environments.Production,
            CreateConfiguration(useMockProviders: false));
        using var provider = services.BuildServiceProvider();

        var paymentException = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<PaymentOptions>>().Value);
        var shippingException = Assert.Throws<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<ShippingOptions>>().Value);

        Assert.Contains("no production payment adapter", paymentException.Message, StringComparison.Ordinal);
        Assert.Contains("no production shipping adapter", shippingException.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Testing", true)]
    [InlineData("Production", false)]
    public void Environment_selects_safe_email_and_identity_senders(string environmentName, bool expectMocks)
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), $"aetkahve-provider-{Guid.NewGuid():N}");
        Directory.CreateDirectory(contentRoot);
        try
        {
            var configuration = CreateConfiguration(
                useMockProviders: false,
                smtpHost: "smtp.test.local",
                fromAddress: "noreply@test.local",
                keyRingPath: Path.Combine(contentRoot, "keys"));
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(environmentName, contentRoot));
            services.AddInfrastructureModule(configuration);
            services.AddCommerceModule(configuration);
            using var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();

            var emailSender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
            var identitySender = scope.ServiceProvider.GetRequiredService<IIdentityMessageSender>();

            if (expectMocks)
            {
                Assert.IsType<MockEmailSender>(emailSender);
                Assert.IsType<InMemoryIdentityMessageSender>(identitySender);
            }
            else
            {
                Assert.IsType<SmtpEmailSender>(emailSender);
                Assert.IsType<OutboxIdentityMessageSender>(identitySender);
            }
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    [Fact]
    public async Task Identity_sender_persists_email_to_outbox_without_external_delivery()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var dbContext = new AppDbContext(dbOptions);
        await dbContext.Database.EnsureCreatedAsync();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "identity@test.local",
            NormalizedUserName = "IDENTITY@TEST.LOCAL",
            Email = "identity@test.local",
            NormalizedEmail = "IDENTITY@TEST.LOCAL",
            FirstName = "Identity",
            LastName = "Test",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        var sender = new OutboxIdentityMessageSender(
            dbContext,
            TimeProvider.System,
            Options.Create(new NotificationOptions { EmailDeliveryEnabled = true }));

        await sender.SendAsync(
            new IdentityMessage(user.Email, "Confirm", "<p>Safe body</p>"),
            default);

        var delivery = await dbContext.NotificationDeliveries.SingleAsync();
        var payload = JsonSerializer.Deserialize<DeliveryPayload>(delivery.PayloadJson);
        Assert.Equal(DeliveryStatus.Pending, delivery.Status);
        Assert.Equal(NotificationChannel.Email, delivery.Channel);
        Assert.Equal(user.Id, delivery.UserId);
        Assert.Equal("Confirm", payload?.Subject);
        Assert.Equal("<p>Safe body</p>", payload?.Body);
    }

    [Fact]
    public async Task Outbox_stops_retrying_after_the_configured_attempt_limit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var dbContext = new AppDbContext(dbOptions);
        await dbContext.Database.EnsureCreatedAsync();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "outbox@test.local",
            NormalizedUserName = "OUTBOX@TEST.LOCAL",
            Email = "outbox@test.local",
            NormalizedEmail = "OUTBOX@TEST.LOCAL",
            FirstName = "Outbox",
            LastName = "Test",
            CreatedAtUtc = DateTimeOffset.UtcNow,
        };
        dbContext.Users.Add(user);
        dbContext.NotificationDeliveries.Add(new NotificationDelivery
        {
            UserId = user.Id,
            Channel = NotificationChannel.Email,
            Destination = user.Email,
            TemplateKey = "Test",
            PayloadJson = JsonSerializer.Serialize(new DeliveryPayload("Subject", "Body")),
            NextAttemptAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();
        var processor = new NotificationDeliveryProcessor(
            dbContext,
            new AlwaysFailEmailSender(),
            new MockSmsSender(),
            Options.Create(new NotificationOptions { MaximumAttempts = 1 }),
            TimeProvider.System,
            NullLogger<NotificationDeliveryProcessor>.Instance);

        await processor.ProcessBatchAsync(default);
        await processor.ProcessBatchAsync(default);

        var delivery = await dbContext.NotificationDeliveries.SingleAsync();
        Assert.Equal(DeliveryStatus.Failed, delivery.Status);
        Assert.Equal(1, delivery.AttemptCount);
        Assert.Null(delivery.NextAttemptAtUtc);
    }

    [Fact]
    public async Task Outbox_claim_rotates_the_token_and_rejects_a_stale_worker()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"aetkahve-outbox-{Guid.NewGuid():N}.db");
        try
        {
            var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={databasePath}")
                .Options;
            Guid deliveryId;
            await using (var setupContext = new AppDbContext(dbOptions))
            {
                await setupContext.Database.EnsureCreatedAsync();
                var user = new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    UserName = "claim@test.local",
                    NormalizedUserName = "CLAIM@TEST.LOCAL",
                    Email = "claim@test.local",
                    NormalizedEmail = "CLAIM@TEST.LOCAL",
                    FirstName = "Claim",
                    LastName = "Test",
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                };
                var delivery = new NotificationDelivery
                {
                    UserId = user.Id,
                    Channel = NotificationChannel.Email,
                    Destination = user.Email,
                    TemplateKey = "Test",
                    PayloadJson = JsonSerializer.Serialize(new DeliveryPayload("Subject", "Body")),
                    NextAttemptAtUtc = DateTimeOffset.UtcNow,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                };
                setupContext.Users.Add(user);
                setupContext.NotificationDeliveries.Add(delivery);
                await setupContext.SaveChangesAsync();
                deliveryId = delivery.Id;
            }

            await using (var firstWorker = new AppDbContext(dbOptions))
            await using (var staleWorker = new AppDbContext(dbOptions))
            {
                var firstClaim = await firstWorker.NotificationDeliveries.SingleAsync(x => x.Id == deliveryId);
                var staleClaim = await staleWorker.NotificationDeliveries.SingleAsync(x => x.Id == deliveryId);
                var originalToken = firstClaim.ConcurrencyToken;
                firstClaim.Status = DeliveryStatus.Processing;
                firstClaim.AttemptCount++;
                firstClaim.UpdatedAtUtc = DateTimeOffset.UtcNow;
                staleClaim.Status = DeliveryStatus.Processing;
                staleClaim.AttemptCount++;
                staleClaim.UpdatedAtUtc = DateTimeOffset.UtcNow;

                await firstWorker.SaveChangesAsync();

                Assert.NotEqual(originalToken, firstClaim.ConcurrencyToken);
                await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => staleWorker.SaveChangesAsync());
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    private static string Protect(IConfiguration configuration, string contentRoot, string plaintext)
    {
        var services = CreateInfrastructureServices(configuration, Environments.Production, contentRoot);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("production-readiness-test")
            .Protect(plaintext);
    }

    private static string Unprotect(IConfiguration configuration, string contentRoot, string protectedPayload)
    {
        var services = CreateInfrastructureServices(configuration, Environments.Production, contentRoot);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IDataProtectionProvider>()
            .CreateProtector("production-readiness-test")
            .Unprotect(protectedPayload);
    }

    private static ServiceCollection CreateInfrastructureServices(
        IConfiguration configuration,
        string environmentName,
        string contentRoot)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(environmentName, contentRoot));
        services.AddInfrastructureModule(configuration);
        return services;
    }

    private static ServiceCollection CreateCommerceServices(string environmentName, IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment(environmentName, AppContext.BaseDirectory));
        services.AddCommerceModule(configuration);
        return services;
    }

    private static IConfiguration CreateConfiguration(
        bool useMockProviders,
        string? smtpHost = null,
        string? fromAddress = null,
        string? keyRingPath = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = "Sqlite",
            ["Database:ConnectionString"] = "Data Source=:memory:",
            ["DataProtection:ApplicationName"] = "AETKAHVE.Tests",
            ["DataProtection:KeyRingPath"] = keyRingPath ?? "App_Data/test-keys",
            ["Notifications:UseMockProviders"] = useMockProviders.ToString(),
            ["Notifications:EmailDeliveryEnabled"] = "true",
            ["Notifications:SmsDeliveryEnabled"] = "false",
            ["Smtp:Host"] = smtpHost,
            ["Smtp:Port"] = "587",
            ["Smtp:UseSsl"] = "true",
            ["Smtp:FromAddress"] = fromAddress ?? "noreply@example.invalid",
            ["Smtp:FromName"] = "AETERNUM RECTUS LUCIS",
        }).Build();

    private sealed class AlwaysFailEmailSender : IEmailSender
    {
        public Task<DeliveryResult> SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
            Task.FromResult(new DeliveryResult(false, null, "Expected test failure."));
    }

    private sealed class TestHostEnvironment(string environmentName, string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "AETKAHVE.Tests";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
