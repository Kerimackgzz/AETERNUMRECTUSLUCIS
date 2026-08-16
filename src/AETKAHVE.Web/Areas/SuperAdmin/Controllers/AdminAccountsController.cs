using System.Security.Claims;
using AETKAHVE.Application.Security;
using AETKAHVE.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AETKAHVE.Web.Areas.SuperAdmin.Controllers;

[Area("SuperAdmin")]
[Authorize(Policy = AuthorizationPolicies.SuperAdminArea)]
[Route("superadmin/admins")]
public sealed class AdminAccountsController(IAdminAccountManagementService adminAccounts) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(
        string? search,
        AdminAccountStatusFilter status = AdminAccountStatusFilter.All,
        int page = 1,
        CancellationToken cancellationToken = default) =>
        View(new AdminAccountsPageViewModel
        {
            Accounts = await adminAccounts.SearchAsync(
                new AdminAccountQuery(search, status, Math.Max(1, page), 25),
                cancellationToken),
            Search = search,
            Status = Enum.IsDefined(status) ? status : AdminAccountStatusFilter.All,
        });

    [HttpGet("create")]
    public IActionResult Create() => View(new AdminAccountCreateViewModel());

    [ValidateAntiForgeryToken]
    [HttpPost("create")]
    public async Task<IActionResult> Create(
        AdminAccountCreateViewModel model,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var started = await adminAccounts.CreateAsync(
            ActorUserId,
            new CreateAdminAccount(model.FirstName, model.LastName, model.Email),
            CreateSecurityContext(),
            cancellationToken);
        if (!started.Result.Succeeded || started.Dispatch is null)
        {
            ModelState.AddModelError(string.Empty, started.Result.Message);
            return View(model);
        }

        var delivery = await QueueTokenEmailAsync(started.Dispatch, cancellationToken);
        TempData[delivery.Succeeded ? "StatusMessage" : "ErrorMessage"] = delivery.Succeeded
            ? "Admin hesabı oluşturuldu ve davet gönderildi."
            : "Admin hesabı oluşturuldu ancak davet gönderilemedi. Listeden daveti yeniden gönderebilirsiniz.";
        return LocalRedirect("/superadmin/admins");
    }

    [HttpGet("{id:guid}/edit")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var account = await adminAccounts.GetAsync(id, cancellationToken);
        return account is null
            ? NotFound()
            : View(new AdminAccountEditViewModel
            {
                Id = account.Id,
                FirstName = account.FirstName,
                LastName = account.LastName,
                Email = account.Email,
            });
    }

    [ValidateAntiForgeryToken]
    [HttpPost("{id:guid}/edit")]
    public async Task<IActionResult> Edit(
        Guid id,
        AdminAccountEditViewModel model,
        CancellationToken cancellationToken)
    {
        model.Id = id;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var updated = await adminAccounts.UpdateAsync(
            ActorUserId,
            id,
            new UpdateAdminAccount(model.FirstName, model.LastName, model.Email),
            CreateSecurityContext(),
            cancellationToken);
        if (!updated.Result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, updated.Result.Message);
            return View(model);
        }

        if (updated.EmailChange is not null)
        {
            var callback = Url.Action(
                "EmailChange",
                "AccountAccess",
                new
                {
                    area = "Admin",
                    userId = updated.EmailChange.UserId,
                    newEmail = updated.EmailChange.NewEmail,
                    token = updated.EmailChange.Token,
                },
                Request.Scheme)!;
            var delivery = await adminAccounts.QueueEmailChangeAsync(
                updated.EmailChange,
                callback,
                cancellationToken);
            TempData[delivery.Succeeded ? "StatusMessage" : "ErrorMessage"] = delivery.Succeeded
                ? "Admin bilgileri güncellendi. Yeni e-posta adresine doğrulama bağlantısı gönderildi."
                : "Admin bilgileri güncellendi ancak e-posta doğrulama bağlantısı gönderilemedi.";
        }
        else
        {
            TempData["StatusMessage"] = updated.Result.Message;
        }

        return LocalRedirect("/superadmin/admins");
    }

    [ValidateAntiForgeryToken]
    [HttpPost("{id:guid}/activate")]
    public Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, true, cancellationToken);

    [ValidateAntiForgeryToken]
    [HttpPost("{id:guid}/deactivate")]
    public Task<IActionResult> Deactivate(Guid id, CancellationToken cancellationToken) =>
        ChangeStatusAsync(id, false, cancellationToken);

    [ValidateAntiForgeryToken]
    [HttpPost("{id:guid}/unlock")]
    public async Task<IActionResult> Unlock(Guid id, CancellationToken cancellationToken)
    {
        var result = await adminAccounts.UnlockAsync(
            ActorUserId,
            id,
            CreateSecurityContext(),
            cancellationToken);
        return RedirectWithResult(result);
    }

    [ValidateAntiForgeryToken]
    [HttpPost("{id:guid}/resend-invitation")]
    public async Task<IActionResult> ResendInvitation(Guid id, CancellationToken cancellationToken)
    {
        var started = await adminAccounts.ResendInvitationAsync(
            ActorUserId,
            id,
            CreateSecurityContext(),
            cancellationToken);
        if (!started.Result.Succeeded || started.Dispatch is null)
        {
            return RedirectWithResult(started.Result);
        }

        var delivery = await QueueTokenEmailAsync(started.Dispatch, cancellationToken);
        TempData[delivery.Succeeded ? "StatusMessage" : "ErrorMessage"] = delivery.Succeeded
            ? "Yeni Admin daveti gönderildi; eski davet bağlantısı geçersizleştirildi."
            : "Yeni davet oluşturuldu ancak e-posta gönderilemedi.";
        return LocalRedirect("/superadmin/admins");
    }

    [ValidateAntiForgeryToken]
    [HttpPost("{id:guid}/password-reset")]
    public async Task<IActionResult> PasswordReset(Guid id, CancellationToken cancellationToken)
    {
        var started = await adminAccounts.BeginPasswordResetAsync(
            ActorUserId,
            id,
            CreateSecurityContext(),
            cancellationToken);
        if (!started.Result.Succeeded || started.Dispatch is null)
        {
            return RedirectWithResult(started.Result);
        }

        var delivery = await QueueTokenEmailAsync(started.Dispatch, cancellationToken);
        TempData[delivery.Succeeded ? "StatusMessage" : "ErrorMessage"] = delivery.Succeeded
            ? "Parola sıfırlama bağlantısı gönderildi."
            : "Parola sıfırlama bağlantısı oluşturuldu ancak e-posta gönderilemedi.";
        return LocalRedirect("/superadmin/admins");
    }

    [HttpGet("{id:guid}/delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var account = await adminAccounts.GetAsync(id, cancellationToken);
        return account is null ? NotFound() : View(account);
    }

    [ValidateAntiForgeryToken]
    [HttpPost("{id:guid}/delete")]
    public async Task<IActionResult> DeleteConfirmed(Guid id, CancellationToken cancellationToken)
    {
        var result = await adminAccounts.DeleteAsync(
            ActorUserId,
            id,
            CreateSecurityContext(),
            cancellationToken);
        return RedirectWithResult(result);
    }

    private async Task<IActionResult> ChangeStatusAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var result = await adminAccounts.SetActiveAsync(
            ActorUserId,
            id,
            isActive,
            CreateSecurityContext(),
            cancellationToken);
        return RedirectWithResult(result);
    }

    private async Task<AdminAccountOperationResult> QueueTokenEmailAsync(
        AdminAccountTokenDispatch dispatch,
        CancellationToken cancellationToken)
    {
        var action = dispatch.Purpose == AdminAccountTokenPurpose.Invitation
            ? "Invitation"
            : "PasswordReset";
        var callback = Url.Action(
            action,
            "AccountAccess",
            new { area = "Admin", userId = dispatch.UserId, token = dispatch.Token },
            Request.Scheme)!;
        return await adminAccounts.QueueTokenEmailAsync(dispatch, callback, cancellationToken);
    }

    private IActionResult RedirectWithResult(AdminAccountOperationResult result)
    {
        TempData[result.Succeeded ? "StatusMessage" : "ErrorMessage"] = result.Message;
        return LocalRedirect("/superadmin/admins");
    }

    private Guid ActorUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id
        : throw new InvalidOperationException("Authenticated SuperAdmin user id is unavailable.");

    private SecurityEventContext CreateSecurityContext() => new(
        HttpContext.Connection.RemoteIpAddress?.ToString(),
        Request.Headers.UserAgent.ToString(),
        Request.Path,
        HttpContext.TraceIdentifier);
}
