using AETKAHVE.Application.Security;
using AETKAHVE.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AETKAHVE.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = AuthorizationPolicies.AdminArea)]
[Route("admin")]
public sealed class HomeController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View(new DashboardSummaryViewModel { Title = "Admin Yönetimi" });
}

