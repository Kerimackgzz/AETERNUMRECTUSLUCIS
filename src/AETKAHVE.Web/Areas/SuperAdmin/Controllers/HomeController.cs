using AETKAHVE.Application.Commerce;
using AETKAHVE.Application.Security;
using AETKAHVE.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AETKAHVE.Web.Areas.SuperAdmin.Controllers;

[Area("SuperAdmin")]
[Authorize(Policy = AuthorizationPolicies.SuperAdminArea)]
[Route("superadmin")]
public sealed class HomeController(IAdminCommerceService adminCommerceService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(new AdminDashboardViewModel(
            "SuperAdmin Yönetimi",
            "Süper Yönetim",
            await adminCommerceService.GetDashboardAsync(cancellationToken)));
}

