using Microsoft.AspNetCore.Mvc;

namespace EDIDashboard.Web.Controllers;

public class WatcherViewController : Controller
{
    public IActionResult Index() => View();
}
