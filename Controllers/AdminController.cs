using Microsoft.AspNetCore.Mvc;

namespace SocialExposure.Controllers
{
    public class AdminController : Controller
    {
        public IActionResult Dashboard()
        {
            return View();
        }
    }
}