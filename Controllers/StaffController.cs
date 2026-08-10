using Microsoft.AspNetCore.Mvc;

namespace SocialExposure.Controllers
{
    public class StaffController : Controller
    {
        [HttpGet]
        public IActionResult Dashboard()
        {
            return View();
        }
    }
}