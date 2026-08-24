using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialExposure.Models;
using SocialExposure.Data;
// VVFVF 
namespace SocialExposure.Controllers
{
    [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff)]
    public class StaffController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StaffController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Dashboard()
        {
            var events = _context.Events
                .Where(e => e.Status != "Completed")
                .OrderByDescending(e => e.Id)
                .ToList();

            return View(events);
        }
        
    }
}

