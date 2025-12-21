using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace portals.Controllers;

[Authorize(Roles = "Radiographer")]
public class RadiographerController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}