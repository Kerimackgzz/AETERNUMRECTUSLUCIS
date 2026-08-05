using AETKAHVE.Application.Commerce;
using AETKAHVE.Application.Security;
using AETKAHVE.Domain.Common;
using AETKAHVE.Web.Models;
using AETKAHVE.Infrastructure.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;

namespace AETKAHVE.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
[Route("checkout")]
public sealed class CheckoutController(ICartService cartService, IAddressService addressService, ICheckoutService checkoutService, IOptions<PaymentOptions> paymentOptions) : CommerceControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = RequiredUserId;
        return View(new CheckoutPageViewModel(await cartService.GetAsync(new CartOwner(userId, null), cancellationToken),
            await addressService.GetAsync(userId, cancellationToken), Guid.NewGuid().ToString("N")));
    }

    [HttpPost]
    public async Task<IActionResult> Initialize([FromBody] CheckoutInput input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(new CommerceMutationResponse(false, "Ödeme bilgileri doğrulanamadı."));
        try
        {
            var callback = Url.Action(nameof(PaymentsController.Callback), "Payments", new { provider = paymentOptions.Value.Provider }, Request.Scheme)
                ?? throw new InvalidOperationException("Payment callback URL could not be generated.");
            var result = await checkoutService.InitializeAsync(new CheckoutRequest(RequiredUserId, input.CartId, input.ShippingAddressId,
                input.BillingAddressId, input.IdempotencyKey, input.CustomerNote, input.PaymentScenario), callback, cancellationToken);
            return Ok(new CommerceMutationResponse(true, "Ödeme başlatıldı.", Data: result));
        }
        catch (CommerceRuleException exception) { return Conflict(new CommerceMutationResponse(false, exception.Message)); }
    }
}

[Route("payments")]
public sealed class PaymentsController(
    ICheckoutService checkoutService,
    IEnumerable<IPaymentWebhookVerifier> paymentWebhookVerifiers,
    IOptions<PaymentOptions> paymentOptions) : Controller
{
    private const int MaximumWebhookBodyBytes = 64 * 1024;

    [HttpGet("{provider}/callback")]
    public Task<IActionResult> Callback(
        string provider,
        [FromQuery] string? reference,
        [FromQuery] string? transactionId,
        [FromQuery] string? status,
        CancellationToken cancellationToken) =>
        AuthenticateAndCompleteAsync(provider, reference, transactionId, status, Request.QueryString.Value ?? string.Empty, cancellationToken);

    [HttpPost("{provider}/callback")]
    [IgnoreAntiforgeryToken]
    [Consumes("application/x-www-form-urlencoded")]
    [RequestSizeLimit(MaximumWebhookBodyBytes)]
    public async Task<IActionResult> Webhook(string provider, CancellationToken cancellationToken)
    {
        Request.EnableBuffering();
        var originalBodyLimit = HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>()?.MaxRequestBodySize;
        using var reader = new StreamReader(Request.Body, Encoding.UTF8, false, 4096, true);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;
        var bodyBytes = Encoding.UTF8.GetByteCount(rawBody);
        if (bodyBytes > MaximumWebhookBodyBytes ||
            (originalBodyLimit is not null && bodyBytes > originalBodyLimit))
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge);
        }

        var form = await Request.ReadFormAsync(cancellationToken);
        return await AuthenticateAndCompleteAsync(
            provider,
            form["reference"].ToString(),
            form["transactionId"].ToString(),
            form["status"].ToString(),
            rawBody,
            cancellationToken);
    }

    private async Task<IActionResult> AuthenticateAndCompleteAsync(
        string provider,
        string? reference,
        string? transactionId,
        string? status,
        string rawBody,
        CancellationToken cancellationToken)
    {
        if (!IsValidCallback(provider, reference, transactionId, status))
        {
            return BadRequest(new CommerceMutationResponse(false, "Payment callback is invalid."));
        }

        if (!provider.Equals(paymentOptions.Value.Provider, StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized(new CommerceMutationResponse(false, "Payment callback could not be verified."));
        }

        var verifier = paymentWebhookVerifiers.SingleOrDefault(candidate =>
            candidate.ProviderName.Equals(provider, StringComparison.OrdinalIgnoreCase));
        if (verifier is null)
        {
            return Unauthorized(new CommerceMutationResponse(false, "Payment callback could not be verified."));
        }

        var effectiveTransactionId = string.IsNullOrWhiteSpace(transactionId) &&
                                     provider.Equals(PaymentProviderNames.Mock, StringComparison.OrdinalIgnoreCase)
            ? $"mock_tx_{Guid.NewGuid():N}"
            : transactionId ?? string.Empty;
        var callback = new PaymentCallbackRequest(reference!, effectiveTransactionId, status!);
        var headers = Request.Headers.ToDictionary(
            header => header.Key,
            header => header.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);
        var authentication = await verifier.AuthenticateAsync(
            new PaymentWebhookEnvelope(provider, Request.Method, callback, rawBody, headers),
            cancellationToken);
        if (!authentication.Succeeded)
        {
            return Unauthorized(new CommerceMutationResponse(false, "Payment callback could not be verified."));
        }

        try
        {
            var result = await checkoutService.CompleteAsync(provider, callback, cancellationToken);
            return Ok(result);
        }
        catch (CommerceRuleException exception) { return Conflict(new CommerceMutationResponse(false, exception.Message)); }
    }

    private static bool IsValidCallback(string provider, string? reference, string? transactionId, string? status) =>
        !string.IsNullOrWhiteSpace(provider) && provider.Length <= 80 &&
        !string.IsNullOrWhiteSpace(reference) && reference.Length <= 160 &&
        (transactionId is null || transactionId.Length <= 160) &&
        !string.IsNullOrWhiteSpace(status) && status.Length <= 80;
}
