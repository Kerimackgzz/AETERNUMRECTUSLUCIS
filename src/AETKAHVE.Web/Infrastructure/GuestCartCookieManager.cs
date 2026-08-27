using AETKAHVE.Infrastructure.Options;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AETKAHVE.Web.Infrastructure;

public sealed class GuestCartCookieManager(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<CommerceOptions> commerceOptions,
    IOptions<SecurityOptions> securityOptions)
{
    public const string CookieName = "AETKAHVE.GuestCart";
    public const string ProtectorPurpose = "AETKAHVE.Commerce.GuestCart.v1";

    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector(ProtectorPurpose);
    private readonly CommerceOptions _commerceOptions = commerceOptions.Value;
    private readonly SecurityOptions _securityOptions = securityOptions.Value;

    public bool HasCookie(HttpRequest request) => request.Cookies.ContainsKey(CookieName);

    public bool TryRead(HttpRequest request, out Guid guestToken)
    {
        guestToken = default;
        if (!request.Cookies.TryGetValue(CookieName, out var protectedToken) ||
            string.IsNullOrWhiteSpace(protectedToken) ||
            protectedToken.Length > 2048)
        {
            return false;
        }

        return TryUnprotect(protectedToken, out guestToken);
    }

    public bool TryUnprotect(string protectedToken, out Guid guestToken)
    {
        guestToken = default;
        if (string.IsNullOrWhiteSpace(protectedToken) || protectedToken.Length > 2048)
        {
            return false;
        }

        try
        {
            return Guid.TryParse(_protector.Unprotect(protectedToken), out guestToken);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void Issue(HttpContext context, Guid guestToken) =>
        context.Response.Cookies.Append(
            CookieName,
            _protector.Protect(guestToken.ToString("D")),
            CreateOptions(context));

    public void Delete(HttpContext context) =>
        context.Response.Cookies.Delete(CookieName, CreateOptions(context, includeLifetime: false));

    public string Protect(Guid guestToken) => _protector.Protect(guestToken.ToString("D"));

    private CookieOptions CreateOptions(HttpContext context, bool includeLifetime = true)
    {
        var options = new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            Path = "/",
            SameSite = SameSiteMode.Lax,
            Secure = _securityOptions.CookieSecurePolicy switch
            {
                CookieSecurePolicy.Always => true,
                CookieSecurePolicy.None => false,
                _ => context.Request.IsHttps,
            },
        };

        if (includeLifetime)
        {
            options.MaxAge = TimeSpan.FromDays(_commerceOptions.GuestCartLifetimeDays);
        }

        return options;
    }
}
