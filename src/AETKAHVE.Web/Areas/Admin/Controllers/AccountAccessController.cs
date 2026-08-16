using AETKAHVE.Application.Security;
using AETKAHVE.Infrastructure.DependencyInjection;
using AETKAHVE.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AETKAHVE.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Route("admin")]
public sealed class AccountAccessController(IAdminAccountManagementService adminAccounts) : Controller
{
    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpGet("invitation")]
    public async Task<IActionResult> Invitation(
        Guid userId,
        string token,
        CancellationToken cancellationToken)
    {
        ApplySensitiveLinkHeaders();
        var validation = await adminAccounts.ValidateTokenAsync(
            userId,
            token,
            AdminAccountTokenPurpose.Invitation,
            cancellationToken);
        return View(new AdminAccountPasswordTokenViewModel
        {
            UserId = userId,
            Token = token ?? string.Empty,
            MaskedEmail = validation.MaskedEmail,
            CanContinue = validation.CanContinue,
            IsActive = validation.IsActive,
        });
    }

    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(SecurityRateLimitPolicies.PasswordRecovery)]
    [HttpPost("invitation")]
    public async Task<IActionResult> CompleteInvitation(
        AdminAccountPasswordTokenViewModel model,
        CancellationToken cancellationToken)
    {
        ApplySensitiveLinkHeaders();
        if (!ModelState.IsValid)
        {
            await RefreshTokenValidationAsync(model, AdminAccountTokenPurpose.Invitation, cancellationToken);
            return View("Invitation", model);
        }

        var result = await adminAccounts.CompleteInvitationAsync(
            model.UserId,
            model.Token,
            model.Password,
            CreateSecurityContext(),
            cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            await RefreshTokenValidationAsync(model, AdminAccountTokenPurpose.Invitation, cancellationToken);
            return View("Invitation", model);
        }

        TempData["StatusMessage"] = result.Message;
        return LocalRedirect("/admin/login");
    }

    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpGet("password-reset")]
    public async Task<IActionResult> PasswordReset(
        Guid userId,
        string token,
        CancellationToken cancellationToken)
    {
        ApplySensitiveLinkHeaders();
        var validation = await adminAccounts.ValidateTokenAsync(
            userId,
            token,
            AdminAccountTokenPurpose.PasswordReset,
            cancellationToken);
        return View(new AdminAccountPasswordTokenViewModel
        {
            UserId = userId,
            Token = token ?? string.Empty,
            MaskedEmail = validation.MaskedEmail,
            CanContinue = validation.CanContinue,
            IsActive = validation.IsActive,
        });
    }

    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(SecurityRateLimitPolicies.PasswordRecovery)]
    [HttpPost("password-reset")]
    public async Task<IActionResult> CompletePasswordReset(
        AdminAccountPasswordTokenViewModel model,
        CancellationToken cancellationToken)
    {
        ApplySensitiveLinkHeaders();
        if (!ModelState.IsValid)
        {
            await RefreshTokenValidationAsync(model, AdminAccountTokenPurpose.PasswordReset, cancellationToken);
            return View("PasswordReset", model);
        }

        var result = await adminAccounts.CompletePasswordResetAsync(
            model.UserId,
            model.Token,
            model.Password,
            CreateSecurityContext(),
            cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            await RefreshTokenValidationAsync(model, AdminAccountTokenPurpose.PasswordReset, cancellationToken);
            return View("PasswordReset", model);
        }

        TempData["StatusMessage"] = result.Message;
        return LocalRedirect("/admin/login?reason=credentials-changed");
    }

    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpGet("email-change/confirm")]
    public async Task<IActionResult> EmailChange(
        Guid userId,
        string newEmail,
        string token,
        CancellationToken cancellationToken)
    {
        ApplySensitiveLinkHeaders();
        var validation = await adminAccounts.ValidateEmailChangeAsync(
            userId,
            newEmail,
            token,
            cancellationToken);
        return View(new AdminAccountEmailChangeViewModel
        {
            UserId = userId,
            NewEmail = newEmail ?? string.Empty,
            Token = token ?? string.Empty,
            MaskedEmail = validation.MaskedEmail,
            CanConfirm = validation.CanContinue,
        });
    }

    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(SecurityRateLimitPolicies.PasswordRecovery)]
    [HttpPost("email-change/confirm")]
    public async Task<IActionResult> ConfirmEmailChange(
        AdminAccountEmailChangeViewModel model,
        CancellationToken cancellationToken)
    {
        ApplySensitiveLinkHeaders();
        var result = await adminAccounts.ConfirmEmailChangeAsync(
            model.UserId,
            model.NewEmail,
            model.Token,
            CreateSecurityContext(),
            cancellationToken);
        if (!result.Result.Succeeded)
        {
            var validation = await adminAccounts.ValidateEmailChangeAsync(
                model.UserId,
                model.NewEmail,
                model.Token,
                cancellationToken);
            model.CanConfirm = validation.CanContinue;
            model.MaskedEmail = validation.MaskedEmail;
            model.StatusMessage = result.Result.Message;
            return View("EmailChange", model);
        }

        if (result.InvitationDispatch is not null)
        {
            var callback = Url.Action(
                nameof(Invitation),
                "AccountAccess",
                new
                {
                    area = "Admin",
                    userId = result.InvitationDispatch.UserId,
                    token = result.InvitationDispatch.Token,
                },
                Request.Scheme)!;
            await adminAccounts.QueueTokenEmailAsync(
                result.InvitationDispatch,
                callback,
                cancellationToken);
        }

        TempData["StatusMessage"] = result.Result.Message;
        return LocalRedirect("/admin/login?reason=credentials-changed");
    }

    private async Task RefreshTokenValidationAsync(
        AdminAccountPasswordTokenViewModel model,
        AdminAccountTokenPurpose purpose,
        CancellationToken cancellationToken)
    {
        var validation = await adminAccounts.ValidateTokenAsync(
            model.UserId,
            model.Token,
            purpose,
            cancellationToken);
        model.CanContinue = validation.CanContinue;
        model.MaskedEmail = validation.MaskedEmail;
        model.IsActive = validation.IsActive;
    }

    private void ApplySensitiveLinkHeaders()
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
    }

    private SecurityEventContext CreateSecurityContext() => new(
        HttpContext.Connection.RemoteIpAddress?.ToString(),
        Request.Headers.UserAgent.ToString(),
        Request.Path,
        HttpContext.TraceIdentifier);
}
