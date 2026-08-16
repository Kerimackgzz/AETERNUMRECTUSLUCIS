using AETKAHVE.Application.Security;
using AETKAHVE.Infrastructure.DependencyInjection;
using AETKAHVE.Infrastructure.Security;
using AETKAHVE.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AETKAHVE.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Route("admin")]
public sealed class AuthController(AuthenticationSessionService authenticationSessions) : Controller
{
    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null, string? reason = null)
    {
        ApplyLoginReason(reason);
        return View(new AdminLoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [EnableRateLimiting(SecurityRateLimitPolicies.AdminLogin)]
    [HttpPost("login")]
    public async Task<IActionResult> Login(AdminLoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var outcome = await authenticationSessions.PasswordSignInAsync(
            HttpContext,
            new SignInAttempt(
                model.Email,
                model.Password,
                model.RememberMe,
                AuthenticationPortal.Admin,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString(),
                Request.Path,
                HttpContext.TraceIdentifier),
            cancellationToken);
        if (!outcome.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Giriş bilgileri geçersiz veya hesap kullanılamıyor.");
            return View(model);
        }

        return !string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
            ? LocalRedirect(model.ReturnUrl)
            : LocalRedirect("/admin");
    }

    [Authorize(Policy = AuthorizationPolicies.AdminArea)]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var portal = User.HasClaim(SecurityClaimTypes.Portal, AuthenticationPortal.SuperAdmin.ToString())
            ? AuthenticationPortal.SuperAdmin
            : AuthenticationPortal.Admin;
        await authenticationSessions.SignOutAsync(HttpContext, portal, "UserLogout", cancellationToken);
        TempData["StatusMessage"] = "Güvenli çıkış yapıldı.";
        return LocalRedirect(portal == AuthenticationPortal.SuperAdmin ? "/superadmin/login" : "/admin/login");
    }

    [AllowAnonymous]
    [HttpGet("access-denied")]
    public IActionResult AccessDenied() => View();

    private void ApplyLoginReason(string? reason)
    {
        if (TempData.ContainsKey("StatusMessage") || TempData.ContainsKey("ErrorMessage"))
        {
            return;
        }

        var message = reason switch
        {
            "expired" => "Oturumunuz hareketsizlik nedeniyle sona erdi. Lütfen yeniden giriş yapın.",
            "session-ended" => "Oturumunuz sona erdi. Lütfen yeniden giriş yapın.",
            "credentials-changed" => "Güvenlik bilgileriniz değiştiği için tüm oturumlar kapatıldı. Lütfen yeniden giriş yapın.",
            _ => null,
        };
        if (message is not null)
        {
            TempData["InfoMessage"] = message;
        }
    }
}
