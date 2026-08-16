using AETKAHVE.Application.Commerce;
using AETKAHVE.Application.Security;
using AETKAHVE.Infrastructure.DependencyInjection;
using AETKAHVE.Infrastructure.Identity;
using AETKAHVE.Infrastructure.Security;
using AETKAHVE.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AETKAHVE.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminArea)]
[Route("admin/security")]
public sealed class SecurityController(
    UserManager<ApplicationUser> userManager,
    IAccountCredentialService credentials,
    AuthenticationSessionService authenticationSessions,
    SecurityAuditWriter auditWriter) : Controller
{
    private const string PortalName = "Admin";
    private const string ConfirmationPath = "/admin/security/email-change/confirm";
    private const string LoginPath = "/admin/login";

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var model = await CreateViewModelAsync(cancellationToken: cancellationToken);
        return model is null ? Unauthorized() : View(model);
    }

    [ValidateAntiForgeryToken]
    [EnableRateLimiting(SecurityRateLimitPolicies.PasswordRecovery)]
    [HttpPost("email-change")]
    public async Task<IActionResult> BeginEmailChange(
        [Bind(Prefix = "EmailChange")] ManagementEmailChangeInput input,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        if (!ModelState.IsValid)
            return View("Index", await CreateViewModelAsync(input, errorSection: "email", cancellationToken: cancellationToken));

        var result = await credentials.BeginEmailChangeAsync(
            user.Id,
            input.CurrentPassword,
            input.NewEmail,
            cancellationToken);
        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.Token))
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View("Index", await CreateViewModelAsync(input, errorSection: "email", cancellationToken: cancellationToken));
        }

        var callback = Url.Action(
            nameof(ConfirmEmailChange),
            "Security",
            new { area = "Admin", userId = user.Id, newEmail = input.NewEmail.Trim(), token = result.Token },
            Request.Scheme)!;
        await credentials.QueueEmailChangeConfirmationAsync(
            user.Id,
            input.NewEmail,
            callback,
            cancellationToken);
        await AuditAsync(user.Id, "ManagementEmailChangeRequested", "A management email change was requested.", cancellationToken);

        TempData["StatusMessage"] = "Doğrulama bağlantısı gönderildi. Yeni e-posta kutunuzu kontrol edin.";
        return LocalRedirect("/admin/security#email-security");
    }

    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpGet("email-change/confirm")]
    public async Task<IActionResult> ConfirmEmailChange(
        Guid userId,
        string newEmail,
        string token,
        CancellationToken cancellationToken)
    {
        ApplySensitiveLinkHeaders();
        var validation = await credentials.ValidateEmailChangeAsync(userId, newEmail, token, cancellationToken);
        return View(CreateConfirmationModel(userId, newEmail, token, validation));
    }

    [AllowAnonymous]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(SecurityRateLimitPolicies.PasswordRecovery)]
    [HttpPost("email-change/confirm")]
    public async Task<IActionResult> ConfirmEmailChangePost(
        ManagementEmailChangeConfirmViewModel input,
        CancellationToken cancellationToken)
    {
        ApplySensitiveLinkHeaders();
        var result = await credentials.ConfirmEmailChangeAsync(
            input.UserId,
            input.NewEmail,
            input.Token,
            cancellationToken);
        if (!result.Succeeded)
        {
            var validation = await credentials.ValidateEmailChangeAsync(
                input.UserId,
                input.NewEmail,
                input.Token,
                cancellationToken);
            return View("ConfirmEmailChange", CreateConfirmationModel(
                input.UserId,
                input.NewEmail,
                input.Token,
                validation,
                result.Message));
        }

        await AuditAsync(input.UserId, "ManagementEmailChanged", "A management email address was changed.", cancellationToken);
        await authenticationSessions.SignOutAllManagementAsync(
            HttpContext,
            input.UserId,
            "EmailChanged",
            cancellationToken);
        TempData["StatusMessage"] = result.Message;
        return LocalRedirect(LoginPath + "?reason=credentials-changed");
    }

    [ValidateAntiForgeryToken]
    [EnableRateLimiting(SecurityRateLimitPolicies.PasswordRecovery)]
    [HttpPost("password")]
    public async Task<IActionResult> ChangePassword(
        [Bind(Prefix = "PasswordChange")] ManagementPasswordChangeInput input,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        if (!ModelState.IsValid)
            return View("Index", await CreateViewModelAsync(passwordChange: input, errorSection: "password", cancellationToken: cancellationToken));

        var result = await credentials.ChangePasswordAsync(
            user.Id,
            input.CurrentPassword,
            input.NewPassword,
            cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View("Index", await CreateViewModelAsync(passwordChange: input, errorSection: "password", cancellationToken: cancellationToken));
        }

        await AuditAsync(user.Id, "ManagementPasswordChanged", "A management password was changed.", cancellationToken);
        await authenticationSessions.SignOutAllManagementAsync(
            HttpContext,
            user.Id,
            "PasswordChanged",
            cancellationToken);
        TempData["StatusMessage"] = result.Message;
        return LocalRedirect(LoginPath + "?reason=credentials-changed");
    }

    private async Task<ManagementSecurityViewModel?> CreateViewModelAsync(
        ManagementEmailChangeInput? emailChange = null,
        ManagementPasswordChangeInput? passwordChange = null,
        string? errorSection = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.GetUserAsync(User);
        if (user is null) return null;
        var roles = await userManager.GetRolesAsync(user);
        var displayName = string.Join(' ', new[] { user.FirstName, user.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return new ManagementSecurityViewModel
        {
            Details = new ManagementSecurityDetails(
                user.Id,
                string.IsNullOrWhiteSpace(displayName) ? PortalName : displayName,
                user.Email ?? string.Empty,
                user.LastLoginAtUtc,
                roles.OrderBy(x => x, StringComparer.Ordinal).ToArray()),
            PortalName = PortalName,
            BasePath = "/admin/security",
            ErrorSection = errorSection,
            EmailChange = emailChange ?? new ManagementEmailChangeInput(),
            PasswordChange = passwordChange ?? new ManagementPasswordChangeInput(),
        };
    }

    private static ManagementEmailChangeConfirmViewModel CreateConfirmationModel(
        Guid userId,
        string? newEmail,
        string? token,
        CustomerEmailChangeValidation validation,
        string? statusMessage = null) => new()
    {
        UserId = userId,
        NewEmail = newEmail ?? string.Empty,
        Token = token ?? string.Empty,
        MaskedEmail = validation.MaskedEmail,
        CanConfirm = validation.CanConfirm,
        StatusMessage = statusMessage,
        PortalName = PortalName,
        ConfirmationPath = ConfirmationPath,
        LoginPath = LoginPath,
    };

    private void ApplySensitiveLinkHeaders()
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
    }

    private Task AuditAsync(Guid userId, string action, string description, CancellationToken cancellationToken) =>
        auditWriter.WriteAsync(
            action,
            description,
            userId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            Request.Path,
            HttpContext.TraceIdentifier,
            cancellationToken);
}
