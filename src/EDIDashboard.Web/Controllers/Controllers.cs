using Microsoft.AspNetCore.Mvc;

namespace EDIDashboard.Web.Controllers;

public class DashboardController : Controller
{
    public IActionResult Index() => View();
}
