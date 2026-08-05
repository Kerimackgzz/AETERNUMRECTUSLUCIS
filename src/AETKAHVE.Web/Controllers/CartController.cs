using AETKAHVE.Application.Commerce;
using AETKAHVE.Domain.Common;
using AETKAHVE.Web.Infrastructure;
using AETKAHVE.Infrastructure.Options;
using AETKAHVE.Web.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Web.Controllers;

[Route("cart")]
public sealed class CartController(ICartService cartService, IDataProtectionProvider dataProtectionProvider, IOptions<CommerceOptions> commerceOptions) : CommerceControllerBase
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(GuestCartMergeFilter.ProtectorPurpose);

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
        Guid? guestToken = null;
        if (!HttpContext.Items.ContainsKey(GuestCartMergeFilter.MergedItemKey) && Request.Cookies.TryGetValue(GuestCartMergeFilter.CookieName, out var protectedToken))
        {
            if (protectedToken.Length <= 2048)
            {
                try { guestToken = Guid.Parse(_protector.Unprotect(protectedToken)); }
                catch (Exception) { guestToken = null; }
            }
        }

        if (CurrentUserId is Guid userId)
        {
            if (guestToken is not null)
            {
                await cartService.MergeGuestCartAsync(userId, guestToken.Value, cancellationToken);
                Response.Cookies.Delete(GuestCartMergeFilter.CookieName);
            }
            return new CartOwner(userId, null);
        }

        guestToken ??= Guid.NewGuid();
        Response.Cookies.Append(GuestCartMergeFilter.CookieName, _protector.Protect(guestToken.Value.ToString("D")), new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = Request.IsHttps,
            MaxAge = TimeSpan.FromDays(commerceOptions.Value.GuestCartLifetimeDays),
        });
        return new CartOwner(null, guestToken);
    }
}
