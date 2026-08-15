using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialExposure.Models;

namespace SocialExposure.Controllers
{
    [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff)]
    public class StaffController : Controller
    {
        [HttpGet]
        public IActionResult Dashboard()
        {
            return View();
        }
    }
}
