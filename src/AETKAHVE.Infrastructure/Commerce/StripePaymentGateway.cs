using AETKAHVE.Application.Commerce;
using AETKAHVE.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace AETKAHVE.Infrastructure.Commerce;

public sealed class StripePaymentGateway(IOptions<StripeOptions> options) : IPaymentGateway
{
    private readonly StripeOptions _options = options.Value;

    public string ProviderName => PaymentProviderNames.Stripe;

    public async Task<PaymentInitializationResult> InitializeAsync(PaymentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            return new PaymentInitializationResult(false, string.Empty, null, "STRIPE_NOT_CONFIGURED", "Stripe is not configured.");
        }

        var successUrl = request.CallbackUrl + "?reference={CHECKOUT_SESSION_ID}&transactionId={CHECKOUT_SESSION_ID}&status=success";
        var cancelUrl = request.CallbackUrl + "?reference={CHECKOUT_SESSION_ID}&transactionId={CHECKOUT_SESSION_ID}&status=cancel";

        var createOptions = new SessionCreateOptions
        {
            Mode = "payment",
            ClientReferenceId = request.OrderId.ToString(),
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            PaymentMethodTypes = ["card"],
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = request.Currency.ToLowerInvariant(),
                        UnitAmount = ToMinorUnits(request.Amount),
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"AETERNUM RECTUS LUCIS sipariş {request.OrderId:N}",
                        },
                    },
                },
            ],
        };

        try
        {
            var service = new SessionService(new StripeClient(_options.SecretKey));
            var session = await service.CreateAsync(
                createOptions,
                new RequestOptions { IdempotencyKey = request.IdempotencyKey },
                cancellationToken);
            return new PaymentInitializationResult(true, session.Id, session.Url, null, null);
        }
        catch (StripeException exception)
        {
            return new PaymentInitializationResult(false, string.Empty, null, exception.StripeError?.Code ?? "STRIPE_ERROR", exception.Message);
        }
    }

    public async Task<PaymentVerificationResult> VerifyAsync(PaymentCallbackRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            return new PaymentVerificationResult(false, false, request.TransactionId, 0, string.Empty, "STRIPE_NOT_CONFIGURED", "Stripe is not configured.");
        }

        try
        {
            var service = new SessionService(new StripeClient(_options.SecretKey));
            var getOptions = new SessionGetOptions();
            getOptions.AddExpand("payment_intent");
            var session = await service.GetAsync(request.RequestReference, getOptions, cancellationToken: cancellationToken);

            var succeeded = session.PaymentStatus == "paid";
            var cancelled = !succeeded && (session.Status == "expired" || request.StatusCode.Equals("cancel", StringComparison.OrdinalIgnoreCase));
            var transactionId = session.PaymentIntentId ?? request.TransactionId;
            var amount = FromMinorUnits(session.AmountTotal ?? 0);
            var currency = (session.Currency ?? string.Empty).ToUpperInvariant();

            return new PaymentVerificationResult(
                succeeded,
                cancelled,
                transactionId,
                amount,
                currency,
                session.PaymentStatus ?? "unknown",
                succeeded ? null : cancelled ? "Payment was cancelled." : "Payment was not completed.");
        }
        catch (StripeException exception)
        {
            return new PaymentVerificationResult(false, false, request.TransactionId, 0, string.Empty,
                exception.StripeError?.Code ?? "STRIPE_ERROR", exception.Message);
        }
    }

    public async Task<RefundResult> RefundAsync(RefundRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            return new RefundResult(false, null, "Stripe is not configured.");
        }

        try
        {
            var service = new RefundService(new StripeClient(_options.SecretKey));
            var refund = await service.CreateAsync(
                new RefundCreateOptions
                {
                    PaymentIntent = request.TransactionId,
                    Amount = ToMinorUnits(request.Amount),
                },
                new RequestOptions { IdempotencyKey = request.IdempotencyKey },
                cancellationToken);
            var succeeded = refund.Status is "succeeded" or "pending";
            return new RefundResult(succeeded, refund.Id, succeeded ? null : refund.FailureReason ?? "Stripe refund failed.");
        }
        catch (StripeException exception)
        {
            return new RefundResult(false, null, exception.Message);
        }
    }

    public static long ToMinorUnits(decimal amount) => (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    public static decimal FromMinorUnits(long minorUnits) => minorUnits / 100m;
}
