using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialExposure.Data;
using SocialExposure.Models;

namespace SocialExposure.Controllers
{
    [Authorize(Roles = UserRoles.Client)]
    public class ClientController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ClientController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Dashboard()
        {
            var clientEmail = User.FindFirstValue(ClaimTypes.Email);

            var events = _context.Events
                .Where(e => e.ClientEmail == clientEmail)
                .OrderByDescending(e => e.Id)
                .ToList();

            return View(events);
        }
    }
}
//lklk changes