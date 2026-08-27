using AETKAHVE.Application.Commerce;
using AETKAHVE.Application.Security;
using AETKAHVE.Domain.Common;
using AETKAHVE.Web.Infrastructure;
using AETKAHVE.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AETKAHVE.Web.Controllers;

[AllowAnonymous]
[Route("cart")]
public sealed class CartController(
    ICartService cartService,
    GuestCartCookieManager guestCartCookieManager) : CommerceControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) => View(new CartPageViewModel(await cartService.GetAsync(await ResolveOwnerAsync(cancellationToken), cancellationToken)));

    [HttpPost("items")]
    public Task<IActionResult> Add([FromBody] AddCartItemInput input, CancellationToken cancellationToken) => MutateAsync(owner => cartService.AddAsync(owner, input.ProductId, input.VariantId, input.Quantity, cancellationToken), cancellationToken);

    [HttpPost("items/{itemId:guid}/quantity")]
    public Task<IActionResult> Update(Guid itemId, [FromBody] UpdateCartQuantityInput input, CancellationToken cancellationToken) => MutateAsync(owner => cartService.UpdateQuantityAsync(owner, itemId, input.Quantity, cancellationToken), cancellationToken);

    [HttpPost("items/{itemId:guid}/remove")]
    public Task<IActionResult> Remove(Guid itemId, CancellationToken cancellationToken) => MutateAsync(owner => cartService.RemoveAsync(owner, itemId, cancellationToken), cancellationToken);

    [HttpPost("clear")]
    public Task<IActionResult> Clear(CancellationToken cancellationToken) => MutateAsync(owner => cartService.ClearAsync(owner, cancellationToken), cancellationToken);

    [HttpPost("coupon")]
    public Task<IActionResult> Coupon([FromBody] CouponInput input, CancellationToken cancellationToken) => MutateAsync(owner => cartService.ApplyCouponAsync(owner, input.Code, cancellationToken), cancellationToken);

    [HttpPost("coupon/remove")]
    public Task<IActionResult> RemoveCoupon(CancellationToken cancellationToken) => MutateAsync(owner => cartService.RemoveCouponAsync(owner, cancellationToken), cancellationToken);

    private async Task<IActionResult> MutateAsync(Func<CartOwner, Task<CartSummary>> action, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return BadRequest(new CommerceMutationResponse(false, "İstek doğrulanamadı."));
        try
        {
            var cart = await action(await ResolveOwnerAsync(cancellationToken));
            return Ok(new CommerceMutationResponse(true, "Sepet güncellendi.", cart.ItemCount, cart.Subtotal, cart.GrandTotal));
        }
        catch (CommerceRuleException exception)
        {
            return Conflict(new CommerceMutationResponse(false, exception.Message));
        }
    }

    private async Task<CartOwner> ResolveOwnerAsync(CancellationToken cancellationToken)
    {
        if (User.TryGetCustomerId(out var userId))
        {
            if (!HttpContext.Items.ContainsKey(GuestCartMergeFilter.MergedItemKey) &&
                guestCartCookieManager.HasCookie(Request))
            {
                if (guestCartCookieManager.TryRead(Request, out var token))
                {
                    await cartService.MergeGuestCartAsync(userId, token, cancellationToken);
                }

                guestCartCookieManager.Delete(HttpContext);
            }

            return new CartOwner(userId, null);
        }

        var guestToken = guestCartCookieManager.TryRead(Request, out var existingToken)
            ? existingToken
            : Guid.NewGuid();
        guestCartCookieManager.Issue(HttpContext, guestToken);
        return new CartOwner(null, guestToken);
    }
}
