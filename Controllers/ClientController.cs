using Microsoft.AspNetCore.Mvc;

namespace SocialExposure.Controllers
{
    public class ClientController : Controller
    {
        public IActionResult Dashboard()
        {
            return View();
        }
    }
}