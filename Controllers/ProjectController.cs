using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialExposure.Data;
using SocialExposure.Models;

namespace SocialExposure.Controllers
{
    public class ProjectController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProjectController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================
        // VIEW ALL PROJECTS
        // ==========================

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var projects = await _context.Projects
                .OrderByDescending(x => x.CreatedDate)
                .ToListAsync();

            ViewBag.Clients = await _context.Users
                .Where(x => x.Role == "Client")
                .ToListAsync();

            return View(projects);
        }

        // ==========================
        // CREATE PROJECT PAGE
        // ==========================

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            ViewBag.Clients = await _context.Users
                .Where(x => x.Role == "Client" && x.IsActive)
                .OrderBy(x => x.FullName)
                .ToListAsync();

            return View();
        }

        // ==========================
        // CREATE PROJECT
        // ==========================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Project project)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Clients = await _context.Users
                    .Where(x => x.Role == "Client" && x.IsActive)
                    .OrderBy(x => x.FullName)
                    .ToListAsync();

                return View(project);
            }

            // Check that selected client exists
            var client = await _context.Users
                .FirstOrDefaultAsync(x =>
                    x.Id == project.ClientId &&
                    x.Role == "Client" &&
                    x.IsActive);

            if (client == null)
            {
                ModelState.AddModelError(
                    "ClientId",
                    "Please select a valid client."
                );

                ViewBag.Clients = await _context.Users
                    .Where(x => x.Role == "Client" && x.IsActive)
                    .OrderBy(x => x.FullName)
                    .ToListAsync();

                return View(project);
            }

            project.CreatedDate = DateTime.Now;
            project.Status = "Active";

            _context.Projects.Add(project);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}