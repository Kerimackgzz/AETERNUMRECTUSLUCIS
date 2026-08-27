using AETKAHVE.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AETKAHVE.Web.Areas.SuperAdmin.Controllers;

[Area("SuperAdmin")]
[Authorize(Policy = AuthorizationPolicies.SuperAdminArea)]
[Route("superadmin")]
public sealed class HomeController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}

