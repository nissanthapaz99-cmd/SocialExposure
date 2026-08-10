using Microsoft.AspNetCore.Mvc;

namespace SocialExposure.Controllers
{
    public class EventsController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }
    }
}