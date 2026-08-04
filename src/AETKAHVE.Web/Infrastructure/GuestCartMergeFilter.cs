using System.Security.Claims;
using AETKAHVE.Application.Commerce;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AETKAHVE.Web.Infrastructure;

public sealed class GuestCartMergeFilter(ICartService cartService, IDataProtectionProvider dataProtectionProvider) : IAsyncActionFilter
{
    public const string CookieName = "AETKAHVE.GuestCart";
    public const string ProtectorPurpose = "AETKAHVE.Commerce.GuestCart.v1";
    public const string MergedItemKey = "AETKAHVE.Commerce.GuestCartMerged";
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (Guid.TryParse(context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) &&
            context.HttpContext.Request.Cookies.TryGetValue(CookieName, out var protectedToken))
        {
            if (TryUnprotect(protectedToken, out var guestToken))
                await cartService.MergeGuestCartAsync(userId, guestToken, context.HttpContext.RequestAborted);
            context.HttpContext.Response.Cookies.Delete(CookieName);
            context.HttpContext.Items[MergedItemKey] = true;
        }

        await next();
    }

    public bool TryUnprotect(string protectedToken, out Guid guestToken)
    {
        guestToken = default;
        if (string.IsNullOrWhiteSpace(protectedToken) || protectedToken.Length > 2048) return false;
        try { return Guid.TryParse(_protector.Unprotect(protectedToken), out guestToken); }
        catch (Exception) { return false; }
    }

    public string Protect(Guid guestToken) => _protector.Protect(guestToken.ToString("D"));
}
