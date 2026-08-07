using System.Text.Encodings.Web;
using AETKAHVE.Application.Notifications;
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

namespace AETKAHVE.Web.Controllers;

[Route("account")]
public sealed class AccountController(
    UserManager<ApplicationUser> userManager,
    AuthenticationSessionService authenticationSessions,
    ICustomerRegistrationService customerRegistrations,
    ICustomerPasswordResetService passwordResets,
    ICustomerAccountQueryService customerAccountQueries,
    ICustomerProfileService customerProfiles,
    IIdentityMessageSender messageSender,
    TimeProvider timeProvider) : CommerceControllerBase
{
    private const string GenericSignInError = "Giriş bilgileri geçersiz veya hesap kullanılamıyor.";

    [Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(await CreateAccountPageAsync(cancellationToken));

    [Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
    [ValidateAntiForgeryToken]
    [HttpPost("profile")]
    public async Task<IActionResult> UpdateProfile(
        [Bind(Prefix = "Profile")] CustomerProfileUpdateInput input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View("Index", await CreateAccountPageAsync(cancellationToken, input));

        var result = await customerProfiles.UpdateAsync(
            RequiredUserId,
            new CustomerProfileUpdate(input.FirstName, input.LastName, input.PhoneNumber, input.DateOfBirth),
            cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View("Index", await CreateAccountPageAsync(cancellationToken, input));
        }

        TempData["StatusMessage"] = result.Message;
        return RedirectToAccountFragment("profile");
    }

    [Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpGet("profile/photo")]
    public async Task<IActionResult> ProfilePhoto(CancellationToken cancellationToken)
    {
        var photo = await customerProfiles.OpenPhotoAsync(RequiredUserId, cancellationToken);
        if (photo is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(photo.Content, photo.ContentType);
    }

    [Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(2 * 1024 * 1024 + 64 * 1024)]
    [HttpPost("profile/photo")]
    public async Task<IActionResult> UploadProfilePhoto(
        CustomerProfilePhotoInput input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid || input.Photo is null)
        {
            TempData["ErrorMessage"] = "Bir profil fotoğrafı seçin.";
            return RedirectToAccountFragment("profile-photo");
        }

        await using var stream = input.Photo.OpenReadStream();
        var result = await customerProfiles.SavePhotoAsync(
            RequiredUserId,
            stream,
            input.Photo.Length,
            input.Photo.FileName,
            input.Photo.ContentType,
            cancellationToken);
        TempData[result.Succeeded ? "StatusMessage" : "ErrorMessage"] = result.Message;
        return RedirectToAccountFragment("profile-photo");
    }

    [Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
    [ValidateAntiForgeryToken]
    [HttpPost("profile/photo/delete")]
    public async Task<IActionResult> DeleteProfilePhoto(CancellationToken cancellationToken)
    {
        var result = await customerProfiles.DeletePhotoAsync(RequiredUserId, cancellationToken);
        TempData[result.Succeeded ? "StatusMessage" : "ErrorMessage"] = result.Message;
        return RedirectToAccountFragment("profile-photo");
    }

    [Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(SecurityRateLimitPolicies.PasswordRecovery)]
    [HttpPost("profile/email-change")]
    public async Task<IActionResult> BeginEmailChange(
        [Bind(Prefix = "EmailChange")] CustomerEmailChangeInput input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View("Index", await CreateAccountPageAsync(cancellationToken, emailChange: input));
        var result = await customerProfiles.BeginEmailChangeAsync(
            RequiredUserId,
            input.CurrentPassword,
            input.NewEmail,
            cancellationToken);
        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.Token))
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View("Index", await CreateAccountPageAsync(cancellationToken, emailChange: input));
        }

        var callback = Url.Action(
            nameof(ConfirmEmailChange),
            "Account",
            new { newEmail = input.NewEmail.Trim(), token = result.Token },
            Request.Scheme)!;
        await customerProfiles.QueueEmailChangeConfirmationAsync(
            RequiredUserId,
            input.NewEmail,
            callback,
            cancellationToken);
        TempData["StatusMessage"] = result.Message;
        return RedirectToAccountFragment("email-security");
    }

    [Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [HttpGet("profile/email-change/confirm")]
    public async Task<IActionResult> ConfirmEmailChange(
        string newEmail,
        string token,
        CancellationToken cancellationToken)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        var validation = await customerProfiles.ValidateEmailChangeAsync(
            RequiredUserId,
            newEmail,
            token,
            cancellationToken);
        return View(new CustomerEmailChangeConfirmViewModel
        {
            NewEmail = newEmail ?? string.Empty,
            Token = token ?? string.Empty,
            MaskedEmail = validation.MaskedEmail,
            CanConfirm = validation.CanConfirm,
        });
    }

    [Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(SecurityRateLimitPolicies.PasswordRecovery)]
    [HttpPost("profile/email-change/confirm")]
    public async Task<IActionResult> ConfirmEmailChangePost(
        CustomerEmailChangeConfirmViewModel input,
        CancellationToken cancellationToken)
    {
        var result = await customerProfiles.ConfirmEmailChangeAsync(
            RequiredUserId,
            input.NewEmail,
            input.Token,
            cancellationToken);
        if (!result.Succeeded)
        {
            var validation = await customerProfiles.ValidateEmailChangeAsync(
                RequiredUserId,
                input.NewEmail,
                input.Token,
                cancellationToken);
            return View("ConfirmEmailChange", new CustomerEmailChangeConfirmViewModel
            {
                NewEmail = input.NewEmail,
                Token = input.Token,
                MaskedEmail = validation.MaskedEmail,
                CanConfirm = validation.CanConfirm,
                StatusMessage = result.Message,
            });
        }

        await authenticationSessions.SignOutAsync(HttpContext, AuthenticationPortal.Customer, "EmailChanged", cancellationToken);
        TempData["StatusMessage"] = result.Message;
        return RedirectToAction(nameof(Login));
    }

    [Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting(SecurityRateLimitPolicies.PasswordRecovery)]
    [HttpPost("profile/password")]
    public async Task<IActionResult> ChangePassword(
        [Bind(Prefix = "PasswordChange")] CustomerPasswordChangeInput input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View("Index", await CreateAccountPageAsync(cancellationToken, passwordChange: input));
        var result = await customerProfiles.ChangePasswordAsync(
            RequiredUserId,
            input.CurrentPassword,
            input.NewPassword,
            cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View("Index", await CreateAccountPageAsync(cancellationToken, passwordChange: input));
        }

        await authenticationSessions.SignOutAsync(HttpContext, AuthenticationPortal.Customer, "PasswordChanged", cancellationToken);
        TempData["StatusMessage"] = result.Message;
        return RedirectToAction(nameof(Login));
    }

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
    [EnableRateLimiting(SecurityRateLimitPolicies.CustomerRegistration)]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await customerRegistrations.BeginAsync(
            new BeginCustomerRegistration(
                model.FirstName,
                model.LastName,
                model.Email,
                model.Password,
                timeProvider.GetUtcNow()),
            cancellationToken);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, "Kayıt bilgileri doğrulanamadı.");
            return View(model);
        }

        if (result.Dispatch is not null)
        {
            await SendConfirmationAsync(result.Dispatch, cancellationToken);
        }

        TempData["StatusMessage"] = "Bilgileriniz alındı. Üyeliği tamamlamak için e-posta doğrulama bağlantınızı açın.";
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet("confirm-email")]
    public async Task<IActionResult> ConfirmEmail(
        Guid registrationId,
        string token,
        CancellationToken cancellationToken)
    {
        var validation = await customerRegistrations.ValidateConfirmationAsync(registrationId, token, cancellationToken);
        return View(new ConfirmEmailViewModel
        {
            RegistrationId = registrationId,
            Token = token ?? string.Empty,
            CanConfirm = validation.CanConfirm,
            MaskedEmail = validation.MaskedEmail,
        });
    }

    [AllowAnonymous]
    [HttpPost("confirm-email")]
    public async Task<IActionResult> ConfirmEmailPost(
        ConfirmEmailViewModel model,
        CancellationToken cancellationToken)
    {
        var status = await customerRegistrations.CompleteAsync(
            model.RegistrationId,
            model.Token,
            CreateSecurityEventContext(),
            cancellationToken);
        if (status is RegistrationCompletionStatus.Completed or RegistrationCompletionStatus.AlreadyCompleted)
        {
            TempData["StatusMessage"] = "E-posta adresiniz doğrulandı ve üyeliğiniz oluşturuldu.";
            return RedirectToAction(nameof(Login));
        }

        var validation = await customerRegistrations.ValidateConfirmationAsync(
            model.RegistrationId,
            model.Token,
            cancellationToken);
        return View("ConfirmEmail", new ConfirmEmailViewModel
        {
            RegistrationId = model.RegistrationId,
            Token = model.Token,
            CanConfirm = validation.CanConfirm,
            MaskedEmail = validation.MaskedEmail,
            StatusMessage = status == RegistrationCompletionStatus.Unavailable
                ? "Üyelik şu anda tamamlanamıyor. Lütfen daha sonra yeniden deneyin."
                : "Doğrulama bağlantısı geçersiz, kullanılmış veya süresi dolmuş.",
        });
    }

    [AllowAnonymous]
    [HttpGet("resend-confirmation")]
    public IActionResult ResendConfirmation() => View(new ResendConfirmationViewModel());

    [AllowAnonymous]
    [EnableRateLimiting(SecurityRateLimitPolicies.PasswordRecovery)]
    [HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation(
        ResendConfirmationViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var dispatch = await customerRegistrations.ResendAsync(model.Email, cancellationToken);
        if (dispatch is not null)
        {
            await SendConfirmationAsync(dispatch, cancellationToken);
        }

        TempData["StatusMessage"] = "Hesap uygunsa yeni doğrulama bağlantısı gönderildi.";
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    [HttpGet("forgot-password")]
    public IActionResult ForgotPassword() => View(new ForgotPasswordViewModel());

    [AllowAnonymous]
    [EnableRateLimiting(SecurityRateLimitPolicies.PasswordRecovery)]
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
    [EnableRateLimiting(SecurityRateLimitPolicies.PasswordRecovery)]
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        ResetPasswordViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var succeeded = await passwordResets.ResetAsync(
            model.Email,
            model.Token,
            model.Password,
            CreateSecurityEventContext(),
            cancellationToken);
        if (!succeeded)
        {
            ModelState.AddModelError(string.Empty, "Parola sıfırlama bağlantısı geçersiz veya süresi dolmuş.");
            return View(model);
        }

        TempData["StatusMessage"] = "Parolanız değiştirildi.";
        return RedirectToAction(nameof(Login));
    }

    [Authorize(Policy = AuthorizationPolicies.CustomerOnly)]
    [ValidateAntiForgeryToken]
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

    private SecurityEventContext CreateSecurityEventContext() => new(
        HttpContext.Connection.RemoteIpAddress?.ToString(),
        Request.Headers.UserAgent.ToString(),
        Request.Path,
        HttpContext.TraceIdentifier);

    private async Task SendConfirmationAsync(
        RegistrationDispatch dispatch,
        CancellationToken cancellationToken)
    {
        var callback = Url.Action(
            nameof(ConfirmEmail),
            "Account",
            new { registrationId = dispatch.RegistrationId, token = dispatch.Token },
            Request.Scheme)!;
        await messageSender.SendAsync(
            new IdentityMessage(
                dispatch.Email,
                "E-posta adresinizi doğrulayın",
                $"<p>E-posta adresinizi doğrulayıp üyeliğinizi tamamlamak için <a href=\"{HtmlEncoder.Default.Encode(callback)}\">bağlantıyı açın</a>.</p>"),
            cancellationToken);
    }

    private IActionResult LocalRedirectOr(string? returnUrl, string fallback) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : LocalRedirect(fallback);

    private IActionResult RedirectToAccountFragment(string fragment) =>
        Redirect($"{Url.Action(nameof(Index), "Account")}#{fragment}");

    private async Task<CustomerAccountPageViewModel> CreateAccountPageAsync(
        CancellationToken cancellationToken,
        CustomerProfileUpdateInput? profile = null,
        CustomerEmailChangeInput? emailChange = null,
        CustomerPasswordChangeInput? passwordChange = null)
    {
        var dashboard = await customerAccountQueries.GetDashboardAsync(RequiredUserId, cancellationToken);
        return new CustomerAccountPageViewModel
        {
            Dashboard = dashboard,
            Profile = profile ?? new CustomerProfileUpdateInput
            {
                FirstName = dashboard.Profile.FirstName,
                LastName = dashboard.Profile.LastName,
                PhoneNumber = dashboard.Profile.PhoneNumber,
                DateOfBirth = dashboard.Profile.DateOfBirth,
            },
            EmailChange = emailChange ?? new CustomerEmailChangeInput(),
            PasswordChange = passwordChange ?? new CustomerPasswordChangeInput(),
        };
    }
}
