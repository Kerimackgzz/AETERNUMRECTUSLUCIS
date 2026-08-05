using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AETKAHVE.Application.Commerce;
using AETKAHVE.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Stripe;

namespace AETKAHVE.Infrastructure.Commerce;

public static class PaymentWebhookHeaders
{
    public const string EventId = "X-Payment-Event-Id";
    public const string Timestamp = "X-Payment-Timestamp";
    public const string Signature = "X-Payment-Signature";
}

public interface IPaymentWebhookReplayStore
{
    bool TryReserve(string provider, string eventId, DateTimeOffset expiresAtUtc, DateTimeOffset nowUtc);
}

public sealed class InMemoryPaymentWebhookReplayStore : IPaymentWebhookReplayStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _reservations = new(StringComparer.Ordinal);

    public bool TryReserve(string provider, string eventId, DateTimeOffset expiresAtUtc, DateTimeOffset nowUtc)
    {
        if (_reservations.Count >= 1024)
        {
            foreach (var reservation in _reservations)
            {
                if (reservation.Value <= nowUtc)
                {
                    _reservations.TryRemove(reservation);
                }
            }
        }

        var key = provider.ToUpperInvariant() + '\u001f' + eventId;
        while (true)
        {
            if (!_reservations.TryGetValue(key, out var existing))
            {
                return _reservations.TryAdd(key, expiresAtUtc);
            }

            if (existing > nowUtc)
            {
                return false;
            }

            if (_reservations.TryUpdate(key, expiresAtUtc, existing))
            {
                return true;
            }
        }
    }
}

public sealed class HmacSha256PaymentWebhookVerifier : IPaymentWebhookVerifier
{
    private const int MaximumEventIdLength = 128;
    private const int Sha256HexLength = 64;
    private readonly byte[] _secret;
    private readonly TimeSpan _timestampTolerance;
    private readonly TimeProvider _timeProvider;
    private readonly IPaymentWebhookReplayStore _replayStore;

    public HmacSha256PaymentWebhookVerifier(
        string providerName,
        ReadOnlySpan<byte> secret,
        TimeSpan timestampTolerance,
        TimeProvider timeProvider,
        IPaymentWebhookReplayStore replayStore)
    {
        if (string.IsNullOrWhiteSpace(providerName)) throw new ArgumentException("Provider name is required.", nameof(providerName));
        if (secret.Length < 32) throw new ArgumentException("Webhook secrets must contain at least 32 bytes.", nameof(secret));
        if (timestampTolerance < TimeSpan.FromSeconds(30) || timestampTolerance > TimeSpan.FromMinutes(15))
            throw new ArgumentOutOfRangeException(nameof(timestampTolerance), "Timestamp tolerance must be between 30 seconds and 15 minutes.");

        ProviderName = providerName;
        _secret = secret.ToArray();
        _timestampTolerance = timestampTolerance;
        _timeProvider = timeProvider;
        _replayStore = replayStore;
    }

    public string ProviderName { get; }

    public ValueTask<PaymentWebhookAuthenticationResult> AuthenticateAsync(
        PaymentWebhookEnvelope envelope,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!envelope.Provider.Equals(ProviderName, StringComparison.OrdinalIgnoreCase) ||
            !envelope.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(PaymentWebhookAuthenticationResult.Rejected("UNSUPPORTED_DELIVERY"));
        }

        if (!TryGetHeader(envelope.Headers, PaymentWebhookHeaders.EventId, out var eventId) ||
            !TryGetHeader(envelope.Headers, PaymentWebhookHeaders.Timestamp, out var timestampText) ||
            !TryGetHeader(envelope.Headers, PaymentWebhookHeaders.Signature, out var suppliedSignature) ||
            string.IsNullOrEmpty(envelope.RawBody))
        {
            return ValueTask.FromResult(PaymentWebhookAuthenticationResult.Rejected("MISSING_SECURITY_HEADERS"));
        }

        if (eventId.Length > MaximumEventIdLength || eventId.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_' and not '.'))
        {
            return ValueTask.FromResult(PaymentWebhookAuthenticationResult.Rejected("INVALID_EVENT_ID"));
        }

        if (!long.TryParse(timestampText, NumberStyles.None, CultureInfo.InvariantCulture, out var unixSeconds))
        {
            return ValueTask.FromResult(PaymentWebhookAuthenticationResult.Rejected("INVALID_TIMESTAMP"));
        }

        DateTimeOffset timestamp;
        try
        {
            timestamp = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return ValueTask.FromResult(PaymentWebhookAuthenticationResult.Rejected("INVALID_TIMESTAMP"));
        }

        var now = _timeProvider.GetUtcNow();
        if (timestamp < now - _timestampTolerance || timestamp > now + _timestampTolerance)
        {
            return ValueTask.FromResult(PaymentWebhookAuthenticationResult.Rejected("STALE_TIMESTAMP"));
        }

        var signatureText = suppliedSignature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
            ? suppliedSignature[7..]
            : suppliedSignature;
        if (signatureText.Length != Sha256HexLength)
        {
            return ValueTask.FromResult(PaymentWebhookAuthenticationResult.Rejected("INVALID_SIGNATURE"));
        }

        byte[] signature;
        try
        {
            signature = Convert.FromHexString(signatureText);
        }
        catch (FormatException)
        {
            return ValueTask.FromResult(PaymentWebhookAuthenticationResult.Rejected("INVALID_SIGNATURE"));
        }

        var canonicalPayload = BuildCanonicalPayload(ProviderName, eventId, unixSeconds, envelope.RawBody);
        var expectedSignature = HMACSHA256.HashData(_secret, Encoding.UTF8.GetBytes(canonicalPayload));
        if (!CryptographicOperations.FixedTimeEquals(expectedSignature, signature))
        {
            return ValueTask.FromResult(PaymentWebhookAuthenticationResult.Rejected("INVALID_SIGNATURE"));
        }

        if (!_replayStore.TryReserve(ProviderName, eventId, now + _timestampTolerance, now))
        {
            return ValueTask.FromResult(PaymentWebhookAuthenticationResult.Rejected("REPLAYED_EVENT"));
        }

        return ValueTask.FromResult(PaymentWebhookAuthenticationResult.Accepted());
    }

    public static string BuildCanonicalPayload(string provider, string eventId, long unixSeconds, string rawBody) =>
        string.Create(CultureInfo.InvariantCulture, $"{provider}\n{eventId}\n{unixSeconds}\n{rawBody}");

    private static bool TryGetHeader(IReadOnlyDictionary<string, string> headers, string name, out string value)
    {
        if (headers.TryGetValue(name, out value!) && !string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var header = headers.FirstOrDefault(pair => pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase));
        value = header.Value;
        return !string.IsNullOrWhiteSpace(value);
    }
}

public sealed class MockPaymentWebhookVerifier(IHostEnvironment environment) : IPaymentWebhookVerifier
{
    public string ProviderName => PaymentProviderNames.Mock;

    public ValueTask<PaymentWebhookAuthenticationResult> AuthenticateAsync(
        PaymentWebhookEnvelope envelope,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var allowedEnvironment = environment.IsDevelopment() || environment.IsEnvironment("Testing");
        return ValueTask.FromResult(allowedEnvironment && envelope.Provider.Equals(ProviderName, StringComparison.OrdinalIgnoreCase)
            ? PaymentWebhookAuthenticationResult.Accepted()
            : PaymentWebhookAuthenticationResult.Rejected("MOCK_NOT_ALLOWED"));
    }
}

public sealed class DisabledPaymentWebhookVerifier : IPaymentWebhookVerifier
{
    public string ProviderName => PaymentProviderNames.Disabled;

    public ValueTask<PaymentWebhookAuthenticationResult> AuthenticateAsync(
        PaymentWebhookEnvelope envelope,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(PaymentWebhookAuthenticationResult.Rejected("PAYMENTS_DISABLED"));
    }
}

/// <summary>
/// The GET leg is the customer's browser returning from Stripe Checkout — it carries no signature,
/// so it is only let through to <see cref="IPaymentGateway.VerifyAsync"/>, which re-fetches the
/// Checkout Session from Stripe and is the actual source of truth (CheckoutService also cross-checks
/// amount/currency). The POST leg is Stripe's server-to-server webhook and is authenticated for real
/// via the Stripe-Signature header against Stripe:WebhookSecret.
/// </summary>
public sealed class StripePaymentWebhookVerifier(IOptions<StripeOptions> options) : IPaymentWebhookVerifier
{
    private readonly StripeOptions _options = options.Value;

    public string ProviderName => PaymentProviderNames.Stripe;

    public ValueTask<PaymentWebhookAuthenticationResult> AuthenticateAsync(
        PaymentWebhookEnvelope envelope,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!envelope.Provider.Equals(ProviderName, StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(PaymentWebhookAuthenticationResult.Rejected("UNSUPPORTED_DELIVERY"));
        }

        if (envelope.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(PaymentWebhookAuthenticationResult.Accepted());
        }

        if (string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            return ValueTask.FromResult(PaymentWebhookAuthenticationResult.Rejected("STRIPE_WEBHOOK_NOT_CONFIGURED"));
        }

        var signatureHeader = envelope.Headers
            .FirstOrDefault(pair => pair.Key.Equals("Stripe-Signature", StringComparison.OrdinalIgnoreCase))
            .Value;
        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            return ValueTask.FromResult(PaymentWebhookAuthenticationResult.Rejected("MISSING_SECURITY_HEADERS"));
        }

        try
        {
            // throwOnApiVersionMismatch:false — we only rely on the signature for authenticity, not on
            // the SDK's bundled API version matching the sender's; a version skew must not turn into an
            // unhandled exception (Stripe.net's own event parsing can NRE on unexpected payload shapes).
            EventUtility.ConstructEvent(envelope.RawBody, signatureHeader, _options.WebhookSecret, tolerance: 300, throwOnApiVersionMismatch: false);
            return ValueTask.FromResult(PaymentWebhookAuthenticationResult.Accepted());
        }
        catch (Exception exception) when (exception is StripeException or Newtonsoft.Json.JsonException or NullReferenceException)
        {
            return ValueTask.FromResult(PaymentWebhookAuthenticationResult.Rejected("INVALID_SIGNATURE"));
        }
    }
}
