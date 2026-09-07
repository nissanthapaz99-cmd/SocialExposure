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

        // Client Dashboard
        public async Task<IActionResult> Dashboard()
        {
            // Get the email address of the currently logged-in client
            var clientEmail = User.FindFirstValue(ClaimTypes.Email);

            // If the user's email cannot be found, prevent access
            if (string.IsNullOrWhiteSpace(clientEmail))
            {
                return Unauthorized();
            }

            // Retrieve events belonging to the logged-in client
            var events = await _context.Events
                .Where(e =>
                    e.ClientEmail != null &&
                    e.ClientEmail.ToLower() == clientEmail.ToLower())
                .OrderByDescending(e => e.Id)
                .ToListAsync();

            return View(events);
        }
    }
}
