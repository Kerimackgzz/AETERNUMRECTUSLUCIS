using AETKAHVE.Application.Commerce;
using AETKAHVE.Application.Security;
using AETKAHVE.Domain.Common;
using AETKAHVE.Web.Models;
using AETKAHVE.Infrastructure.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

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
    public async Task<IActionResult> Initialize(CheckoutInput input, CancellationToken cancellationToken)
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
public sealed class PaymentsController(ICheckoutService checkoutService) : Controller
{
    [HttpGet("{provider}/callback")]
    public Task<IActionResult> Callback(string provider, [FromQuery] string reference, [FromQuery] string? transactionId, [FromQuery] string status, CancellationToken cancellationToken) =>
        CompleteAsync(provider, reference, transactionId, status, cancellationToken);

    [HttpPost("{provider}/callback")]
    [IgnoreAntiforgeryToken]
    public Task<IActionResult> Webhook(string provider, [FromForm] string reference, [FromForm] string? transactionId, [FromForm] string status, CancellationToken cancellationToken) =>
        CompleteAsync(provider, reference, transactionId, status, cancellationToken);

    private async Task<IActionResult> CompleteAsync(string provider, string reference, string? transactionId, string status, CancellationToken cancellationToken)
    {
        try
        {
            var result = await checkoutService.CompleteAsync(provider, new PaymentCallbackRequest(reference, transactionId ?? $"mock_tx_{Guid.NewGuid():N}", status), cancellationToken);
            return Ok(result);
        }
        catch (CommerceRuleException exception) { return Conflict(new CommerceMutationResponse(false, exception.Message)); }
    }
}
