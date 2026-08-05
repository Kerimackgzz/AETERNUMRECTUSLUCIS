using System.ComponentModel.DataAnnotations;

namespace AETKAHVE.Infrastructure.Options;

public sealed class CommerceOptions
{
    public const string SectionName = "Commerce";

    [Required, StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "TRY";

    [Range(1, 365)]
    public int GuestCartLifetimeDays { get; set; } = 30;

    [Range(1, 100)]
    public int MaximumCartItemQuantity { get; set; } = 20;

    [Range(0, 1_000_000)]
    public decimal ShippingFee { get; set; }

    [Range(0, 1_000_000)]
    public decimal FreeShippingThreshold { get; set; }

    [Range(1, 365)]
    public int ReturnWindowDays { get; set; } = 14;

    public bool SeedDevelopmentData { get; set; } = true;
}

public sealed class PaymentOptions
{
    public const string SectionName = "Payment";
    [Required] public string Provider { get; set; } = PaymentProviderNames.Disabled;
}

public static class PaymentProviderNames
{
    public const string Disabled = "Disabled";
    public const string Mock = "Mock";
    public const string Stripe = "Stripe";
}

public sealed class StripeOptions
{
    public const string SectionName = "Stripe";
    public string? SecretKey { get; set; }
    public string? PublishableKey { get; set; }
    public string? WebhookSecret { get; set; }
}

public sealed class ShippingOptions
{
    public const string SectionName = "Shipping";
    [Required] public string Provider { get; set; } = "Mock";
    [Required] public string CompanyName { get; set; } = "AETERNUM Mock Shipping";
}

public sealed class InvoiceOptions
{
    public const string SectionName = "Invoice";
    [Required] public string SellerName { get; set; } = "AETERNUM RECTUS LUCIS";
    [Required] public string StorageDirectory { get; set; } = "App_Data/invoices";
}

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";
    public bool UseMockProviders { get; set; } = true;
    public bool AllowExternalDeliveryInDevelopment { get; set; }
    public bool EmailDeliveryEnabled { get; set; } = true;
    public bool SmsDeliveryEnabled { get; set; }
    public bool WorkerEnabled { get; set; } = true;
    [Range(1, 20)] public int MaximumAttempts { get; set; } = 5;
    [Range(1, 100)] public int BatchSize { get; set; } = 20;
    [Range(1, 3600)] public int PollIntervalSeconds { get; set; } = 5;
    [Range(10, 3600)] public int ProcessingLeaseSeconds { get; set; } = 300;
    [Range(1, 1440)] public int MaximumRetryDelayMinutes { get; set; } = 60;
}

public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";
    public string? Host { get; set; }
    [Range(1, 65535)] public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    [EmailAddress] public string FromAddress { get; set; } = "noreply@example.invalid";
    [Required] public string FromName { get; set; } = "AETERNUM RECTUS LUCIS";
    [Range(5, 120)] public int TimeoutSeconds { get; set; } = 30;
}

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";
    [Required] public string RootDirectory { get; set; } = "App_Data/uploads";
    [Range(1024, 20 * 1024 * 1024)] public long MaximumFileBytes { get; set; } = 5 * 1024 * 1024;
    public string[] AllowedExtensions { get; set; } = [".jpg", ".jpeg", ".png", ".webp"];
    public string[] AllowedContentTypes { get; set; } = ["image/jpeg", "image/png", "image/webp"];
}
