using AETKAHVE.Application.Security;
using AETKAHVE.Infrastructure.DependencyInjection;
using AETKAHVE.Infrastructure.Security;
using AETKAHVE.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AETKAHVE.Web.Areas.SuperAdmin.Controllers;

[Area("SuperAdmin")]
[Route("superadmin")]
public sealed class AuthController(AuthenticationSessionService authenticationSessions) : Controller
{
    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null, string? reason = null)
    {
        ApplyLoginReason(reason);
        return View(new SuperAdminLoginViewModel { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [EnableRateLimiting(SecurityRateLimitPolicies.SuperAdminLogin)]
    [HttpPost("login")]
    public async Task<IActionResult> Login(SuperAdminLoginViewModel model, CancellationToken cancellationToken)
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
                AuthenticationPortal.SuperAdmin,
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
            : LocalRedirect("/superadmin");
    }

    [Authorize(Policy = AuthorizationPolicies.SuperAdminArea)]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await authenticationSessions.SignOutAsync(
            HttpContext,
            AuthenticationPortal.SuperAdmin,
            "UserLogout",
            cancellationToken);
        TempData["StatusMessage"] = "Güvenli çıkış yapıldı.";
        return LocalRedirect("/superadmin/login");
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
