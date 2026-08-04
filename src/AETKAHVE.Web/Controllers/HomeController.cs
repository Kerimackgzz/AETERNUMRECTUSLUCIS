using AETKAHVE.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace AETKAHVE.Web.Controllers;

public sealed class HomeController : Controller
{
    public IActionResult Index()
    {
        return View(new HomePageViewModel());
    }

    public IActionResult Privacy()
    {
        return View();
    }

}
