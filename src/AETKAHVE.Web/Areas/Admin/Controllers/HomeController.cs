using AETKAHVE.Application.Commerce;
using AETKAHVE.Application.Security;
using AETKAHVE.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AETKAHVE.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminArea)]
[Route("admin")]
public sealed class HomeController(IAdminCommerceService adminCommerceService) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken) =>
        View(new AdminDashboardViewModel(
            "Admin Yönetimi",
            "Admin",
            await adminCommerceService.GetDashboardAsync(cancellationToken)));
}

