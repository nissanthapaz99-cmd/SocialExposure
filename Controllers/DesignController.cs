using Microsoft.AspNetCore.Mvc;

namespace SocialExposure.Controllers
{
    public class DesignController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
    }
}