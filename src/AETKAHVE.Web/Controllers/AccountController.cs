using System.Text.Encodings.Web;
using AETKAHVE.Application.Notifications;
using AETKAHVE.Application.Security;
using AETKAHVE.Infrastructure.DependencyInjection;
using AETKAHVE.Infrastructure.Identity;
using AETKAHVE.Infrastructure.Security;
using AETKAHVE.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AETKAHVE.Web.Controllers;

[Route("account")]
public sealed class AccountController(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    AuthenticationSessionService authenticationSessions,
    IIdentityMessageSender messageSender,
    TimeProvider timeProvider) : Controller
{
    private const string GenericSignInError = "Giriş bilgileri geçersiz veya hesap kullanılamıyor.";

    [Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
    [HttpGet("")]
    public IActionResult Index() => View(new DashboardSummaryViewModel { Title = "Hesabım" });

    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl = null) => View(new LoginViewModel { ReturnUrl = returnUrl });

    [AllowAnonymous]
    [EnableRateLimiting(SecurityRateLimitPolicies.CustomerLogin)]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var outcome = await authenticationSessions.PasswordSignInAsync(
            HttpContext,
            CreateAttempt(model, AuthenticationPortal.Customer),
            cancellationToken);
        if (!outcome.Succeeded)
        {
            ModelState.AddModelError(string.Empty, GenericSignInError);
            return View(model);
        }

        return LocalRedirectOr(model.ReturnUrl, Url.Action(nameof(Index), "Account")!);
    }

    [AllowAnonymous]
    [HttpGet("register")]
    public IActionResult Register() => View(new RegisterViewModel());

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (!await roleManager.RoleExistsAsync(RoleNames.Customer))
        {
            var roleResult = await roleManager.CreateAsync(new ApplicationRole { Name = RoleNames.Customer });
            if (!roleResult.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "Kayıt şu anda tamamlanamıyor.");
                return View(model);
            }
        }

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = model.Email.Trim(),
            Email = model.Email.Trim(),
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            CreatedAtUtc = timeProvider.GetUtcNow(),
            IsActive = true,
        };
        var result = await userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Kayıt bilgileri doğrulanamadı.");
            return View(model);
        }

        var roleAssignment = await userManager.AddToRoleAsync(user, RoleNames.Customer);
        if (!roleAssignment.Succeeded)
        {
            await userManager.DeleteAsync(user);
            ModelState.AddModelError(string.Empty, "Kayıt şu anda tamamlanamıyor.");
            return View(model);
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var callback = Url.Action(
            nameof(ConfirmEmail),
            "Account",
            new { userId = user.Id, token },
            Request.Scheme)!;
        await messageSender.SendAsync(
            new IdentityMessage(
                user.Email!,
                "E-posta adresinizi doğrulayın",
                $"<p>E-posta adresinizi doğrulamak için <a href=\"{HtmlEncoder.Default.Encode(callback)}\">bağlantıyı açın</a>.</p>"),
            cancellationToken);

        TempData["StatusMessage"] = "Kayıt alındı. E-posta doğrulama bağlantınızı kontrol edin.";
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(Guid userId, string token)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return BadRequest();
        }

        var result = await userManager.ConfirmEmailAsync(user, token);
        TempData["StatusMessage"] = result.Succeeded
            ? "E-posta adresiniz doğrulandı."
            : "Doğrulama bağlantısı geçersiz veya süresi dolmuş.";
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet("forgot-password")]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        if (user is not null && user.IsActive && user.EmailConfirmed)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var callback = Url.Action(
                nameof(ResetPassword),
                "Account",
                new { email = user.Email, token },
                Request.Scheme)!;
            await messageSender.SendAsync(
                new IdentityMessage(
                    user.Email!,
                    "Parolanızı sıfırlayın",
                    $"<p>Parolanızı sıfırlamak için <a href=\"{HtmlEncoder.Default.Encode(callback)}\">bağlantıyı açın</a>.</p>"),
                cancellationToken);
        }

        TempData["StatusMessage"] = "Hesap uygunsa parola sıfırlama bağlantısı gönderildi.";
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet("reset-password")]
    public IActionResult ResetPassword(string email, string token) =>
        View(new ResetPasswordViewModel { Email = email, Token = token });

    [AllowAnonymous]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email.Trim());
        var result = user is null
            ? IdentityResult.Failed()
            : await userManager.ResetPasswordAsync(user, model.Token, model.Password);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Parola sıfırlama bağlantısı geçersiz veya süresi dolmuş.");
            return View(model);
        }

        TempData["StatusMessage"] = "Parolanız değiştirildi.";
        return RedirectToAction(nameof(Login));
    }

    [Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await authenticationSessions.SignOutAsync(HttpContext, AuthenticationPortal.Customer, "UserLogout", cancellationToken);
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    [HttpGet("access-denied")]
    public IActionResult AccessDenied() => View();

    private SignInAttempt CreateAttempt(LoginViewModel model, AuthenticationPortal portal) => new(
        model.Email,
        model.Password,
        model.RememberMe,
        portal,
        HttpContext.Connection.RemoteIpAddress?.ToString(),
        Request.Headers.UserAgent.ToString(),
        Request.Path,
        HttpContext.TraceIdentifier);

    private IActionResult LocalRedirectOr(string? returnUrl, string fallback) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : LocalRedirect(fallback);
}
