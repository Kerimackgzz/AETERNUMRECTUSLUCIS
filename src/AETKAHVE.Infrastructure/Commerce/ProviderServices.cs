using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using AETKAHVE.Application.Commerce;
using AETKAHVE.Domain.Common;
using AETKAHVE.Domain.Commerce;
using AETKAHVE.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Infrastructure.Commerce;

public sealed class MockPaymentGateway : IPaymentGateway
{
    private readonly ConcurrentDictionary<string, PaymentRequest> _requests = new(StringComparer.Ordinal);
    public string ProviderName => "Mock";

    public Task<PaymentInitializationResult> InitializeAsync(PaymentRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var reference = $"mock_{request.PaymentId:N}";
        _requests.TryAdd(reference, request);
        var failed = request.Scenario.Equals("initialize-fail", StringComparison.OrdinalIgnoreCase);
        return Task.FromResult(new PaymentInitializationResult(!failed, reference, failed ? null : $"{request.CallbackUrl}?reference={Uri.EscapeDataString(reference)}&status={Uri.EscapeDataString(request.Scenario)}", failed ? "MOCK_INIT_FAILED" : null, failed ? "Mock initialization failed." : null));
    }

    public Task<PaymentVerificationResult> VerifyAsync(PaymentCallbackRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_requests.TryGetValue(request.RequestReference, out var initialized))
        {
            return Task.FromResult(new PaymentVerificationResult(false, false, request.TransactionId, 0, "TRY", "UNKNOWN_REFERENCE", "Payment reference is unknown."));
        }

        var cancelled = request.StatusCode.Equals("cancel", StringComparison.OrdinalIgnoreCase);
        var failed = request.StatusCode.Equals("fail", StringComparison.OrdinalIgnoreCase);
        return Task.FromResult(new PaymentVerificationResult(!cancelled && !failed, cancelled, request.TransactionId, initialized.Amount, initialized.Currency,
            cancelled ? "CANCELLED" : failed ? "FAILED" : "APPROVED", cancelled ? "Payment was cancelled." : failed ? "Payment failed." : null));
    }

    public Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var failed = request.TransactionId.Contains("refund-fail", StringComparison.OrdinalIgnoreCase);
        return Task.FromResult(new RefundResult(!failed, failed ? null : $"refund_{request.PaymentId:N}_{request.IdempotencyKey}", failed ? "Mock refund failed." : null));
    }
}

public sealed class MockShippingProvider : IShippingProvider
{
    public string ProviderName => "Mock";

    public Task<ShipmentCreateResult> CreateShipmentAsync(ShipmentCreateRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tracking = $"ARL{request.OrderId:N}"[..18].ToUpperInvariant();
        return Task.FromResult(new ShipmentCreateResult(true, tracking, $"https://shipping.example.invalid/track/{tracking}", null));
    }

    public Task<ShipmentTrackingResult> TrackAsync(string trackingNumber, CancellationToken cancellationToken) =>
        Task.FromResult(new ShipmentTrackingResult(true, ShipmentStatus.Shipped, $"{trackingNumber} is in transit."));

    public Task<ShipmentCancelResult> CancelAsync(string trackingNumber, CancellationToken cancellationToken) =>
        Task.FromResult(new ShipmentCancelResult(true, null));
}

public sealed class MockEmailSender : IEmailSender
{
    public ConcurrentQueue<EmailMessage> Sent { get; } = new();

    public Task<DeliveryResult> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Sent.Enqueue(message);
        return Task.FromResult(new DeliveryResult(true, "email_" + StableReference(message.Destination, message.Subject, message.HtmlBody), null));
    }

    private static string StableReference(params string[] values) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001f', values))))[..24].ToLowerInvariant();
}

public sealed class MockSmsSender : ISmsSender
{
    public ConcurrentQueue<SmsMessage> Sent { get; } = new();

    public Task<DeliveryResult> SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Sent.Enqueue(message);
        var reference = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(message.Destination + '\u001f' + message.Body)))[..24].ToLowerInvariant();
        return Task.FromResult(new DeliveryResult(true, "sms_" + reference, null));
    }
}

public sealed class UnconfiguredSmsSender : ISmsSender
{
    public Task<DeliveryResult> SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new DeliveryResult(false, null, "SMS provider is not configured."));
    }
}

public sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task<DeliveryResult> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Host)) return new DeliveryResult(false, null, "SMTP is not configured.");
        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseSsl,
            Credentials = string.IsNullOrWhiteSpace(_options.UserName) ? CredentialCache.DefaultNetworkCredentials : new NetworkCredential(_options.UserName, _options.Password),
        };
        using var mail = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = message.Subject,
            Body = message.HtmlBody,
            IsBodyHtml = true,
        };
        mail.To.Add(message.Destination);
        await client.SendMailAsync(mail, cancellationToken);
        return new DeliveryResult(true, null, null);
    }
}

public sealed class LocalFileStorageService(IOptions<FileStorageOptions> options, Microsoft.Extensions.Hosting.IHostEnvironment environment) : IFileStorageService
{
    private readonly FileStorageOptions _options = options.Value;
    private readonly string _root = ResolveRoot(environment.ContentRootPath, options.Value.RootDirectory);

    public async Task<StoredFile> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (!_options.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) || !_options.AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            throw new CommerceRuleException("File type is not allowed.");
        if (!content.CanRead) throw new CommerceRuleException("File cannot be read.");

        var fileName = $"{Guid.NewGuid():N}{extension}";
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, fileName);
        try
        {
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
            var buffer = new byte[81920];
            var signature = new byte[12];
            long total = 0;
            int signatureLength = 0;
            int read;
            while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
            {
                total += read;
                if (total > _options.MaximumFileBytes) throw new CommerceRuleException("File is too large.");
                if (signatureLength < signature.Length)
                {
                    var count = Math.Min(read, signature.Length - signatureLength);
                    buffer.AsSpan(0, count).CopyTo(signature.AsSpan(signatureLength));
                    signatureLength += count;
                }
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            if (!IsValidImageSignature(extension, contentType, signature.AsSpan(0, signatureLength)))
                throw new CommerceRuleException("File content does not match its declared image type.");
            await output.FlushAsync(cancellationToken);
            return new StoredFile(fileName, contentType, total);
        }
        catch
        {
            if (File.Exists(path)) File.Delete(path);
            throw;
        }
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = SafePath(_root, storageKey);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = SafePath(_root, storageKey);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    private static string ResolveRoot(string contentRoot, string configured) => Path.GetFullPath(Path.IsPathRooted(configured) ? configured : Path.Combine(contentRoot, configured));
    private static bool IsValidImageSignature(string extension, string contentType, ReadOnlySpan<byte> bytes) => (extension, contentType.ToLowerInvariant()) switch
    {
        (".jpg" or ".jpeg", "image/jpeg") => bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF,
        (".png", "image/png") => bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        (".webp", "image/webp") => bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8),
        _ => false,
    };

    internal static string SafePath(string root, string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey) || Path.GetFileName(storageKey) != storageKey) throw new CommerceRuleException("Storage key is invalid.");
        var path = Path.GetFullPath(Path.Combine(root, storageKey));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new CommerceRuleException("Storage path is invalid.");
        return path;
    }
}

public sealed class LocalInvoiceStorage(IOptions<InvoiceOptions> options, Microsoft.Extensions.Hosting.IHostEnvironment environment) : IInvoiceStorage
{
    private readonly string _root = Path.GetFullPath(Path.IsPathRooted(options.Value.StorageDirectory) ? options.Value.StorageDirectory : Path.Combine(environment.ContentRootPath, options.Value.StorageDirectory));

    public async Task<string> SaveAsync(string invoiceNumber, ReadOnlyMemory<byte> content, CancellationToken cancellationToken)
    {
        var safeName = string.Concat(invoiceNumber.Where(x => char.IsLetterOrDigit(x) || x is '-' or '_')) + ".pdf";
        Directory.CreateDirectory(_root);
        var path = LocalFileStorageService.SafePath(_root, safeName);
        await File.WriteAllBytesAsync(path, content.ToArray(), cancellationToken);
        return safeName;
    }

    public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = LocalFileStorageService.SafePath(_root, storageKey);
        return Task.FromResult<Stream?>(File.Exists(path) ? File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null);
    }
}
