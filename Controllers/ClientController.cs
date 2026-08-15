using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialExposure.Models;

namespace SocialExposure.Controllers
{
    [Authorize(Roles = UserRoles.Client)]
    public class ClientController : Controller
    {
        public IActionResult Dashboard()
        {
            return View();
        }
    }
}
