using AETKAHVE.Application.Commerce;
using AETKAHVE.Application.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AETKAHVE.Web.Infrastructure;

public sealed class GuestCartMergeFilter(
    ICartService cartService,
    GuestCartCookieManager guestCartCookieManager) : IAsyncActionFilter
{
    public const string CookieName = GuestCartCookieManager.CookieName;
    public const string ProtectorPurpose = GuestCartCookieManager.ProtectorPurpose;
    public const string MergedItemKey = "AETKAHVE.Commerce.GuestCartMerged";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var httpContext = context.HttpContext;
        if (httpContext.User.TryGetCustomerId(out var userId) &&
            guestCartCookieManager.HasCookie(httpContext.Request))
        {
            if (guestCartCookieManager.TryRead(httpContext.Request, out var guestToken))
            {
                var merge = await cartService.MergeGuestCartAsync(
                    userId,
                    guestToken,
                    httpContext.RequestAborted);
                if (merge.Warnings.Count > 0 && context.Controller is Controller controller)
                {
                    controller.TempData["InfoMessage"] = string.Join(" ", merge.Warnings);
                }
            }

            guestCartCookieManager.Delete(httpContext);
            httpContext.Items[MergedItemKey] = true;
        }

        await next();
    }
}
