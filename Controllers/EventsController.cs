using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SocialExposure.Data;
using SocialExposure.Models;

namespace SocialExposure.Controllers
{
    [Authorize]
    public class EventsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public EventsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================
        // EVENTS MAIN PAGE
        // =========================
        public IActionResult Index()
        {
            var role = User.FindFirstValue(ClaimTypes.Role);

            // Staff/Admin can see all events
            if (role == UserRoles.Admin || role == UserRoles.Staff)
            {
                var events = _context.Events
                    .OrderByDescending(e => e.Id)
                    .ToList();

                return View(events);
            }

            // Client only sees events assigned to their email
            var clientEmail = User.FindFirstValue(ClaimTypes.Email);

            var clientEvents = _context.Events
                .Where(e => e.ClientEmail == clientEmail)
                .OrderByDescending(e => e.Id)
                .ToList();

            return View(clientEvents);
        }

        // =========================
        // CREATE EVENT - GET
        // =========================
        [HttpGet]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff)]
        public IActionResult Create()
        {
            return View();
        }

        // =========================
        // CREATE EVENT - POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff)]
        public IActionResult Create(Event model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            model.Status = string.IsNullOrWhiteSpace(model.Status)
                ? "Pending"
                : model.Status;

            _context.Events.Add(model);
            _context.SaveChanges();

            return RedirectToAction(nameof(ViewStaff));
        }

        // =========================
        // CLIENT EVENTS PAGE
        // =========================
        [Authorize(Roles = UserRoles.Client)]
        public IActionResult Client()
        {
            var clientEmail = User.FindFirstValue(ClaimTypes.Email);

            var events = _context.Events
                .Where(e => e.ClientEmail == clientEmail)
                .OrderByDescending(e => e.Id)
                .ToList();

            return View("Index", events);
        }

        // =========================
        // CLIENT VIEW EVENTS
        // =========================
        [Authorize(Roles = UserRoles.Client)]
        public IActionResult ViewClient()
        {
            var clientEmail = User.FindFirstValue(ClaimTypes.Email);

            var events = _context.Events
                .Where(e => e.ClientEmail == clientEmail)
                .OrderByDescending(e => e.Id)
                .ToList();

            return View(events);
        }

        // =========================
        // STAFF VIEW EVENTS
        // =========================
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff)]
        public IActionResult ViewStaff()
        {
            var events = _context.Events
                .OrderByDescending(e => e.Id)
                .ToList();

            return View(events);
        }

        // =========================
        // EVENT DETAILS
        // =========================
        public IActionResult Details(int id)
        {
            var eventItem = _context.Events
                .FirstOrDefault(e => e.Id == id);

            if (eventItem == null)
            {
                return NotFound();
            }

            var role = User.FindFirstValue(ClaimTypes.Role);

            // Prevent a client from opening another client's event manually
            if (role == UserRoles.Client)
            {
                var clientEmail = User.FindFirstValue(ClaimTypes.Email);

                if (!string.Equals(
                        eventItem.ClientEmail,
                        clientEmail,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }
            }

            return View(eventItem);
        }

        // =========================
        // EDIT EVENT - GET
        // =========================
        [HttpGet]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff)]
        public IActionResult Edit(int id)
        {
            var eventItem = _context.Events
                .FirstOrDefault(e => e.Id == id);

            if (eventItem == null)
            {
                return NotFound();
            }

            return View(eventItem);
        }

        // =========================
        // EDIT EVENT - POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff)]
        public IActionResult Edit(Event model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var eventItem = _context.Events
                .FirstOrDefault(e => e.Id == model.Id);

            if (eventItem == null)
            {
                return NotFound();
            }

            eventItem.EventName = model.EventName;
            eventItem.ClientName = model.ClientName;
            eventItem.ClientEmail = model.ClientEmail;
            eventItem.Description = model.Description;
            eventItem.StartDate = model.StartDate;
            eventItem.Deadline = model.Deadline;
            eventItem.Status = model.Status;

            _context.SaveChanges();

            return RedirectToAction(nameof(ViewStaff));
        }

        // =========================
        // DELETE EVENT
        // =========================
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff)]
        public IActionResult Delete(int id)
        {
            var eventItem = _context.Events
                .FirstOrDefault(e => e.Id == id);

            if (eventItem == null)
            {
                return NotFound();
            }

            _context.Events.Remove(eventItem);
            _context.SaveChanges();

            return RedirectToAction(nameof(ViewStaff));
        }

        // =========================
        // UPLOAD DESIGN
        // =========================
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff)]
        public IActionResult Upload(int id)
        {
            var eventItem = _context.Events
                .FirstOrDefault(e => e.Id == id);

            if (eventItem == null)
            {
                return NotFound();
            }

            return View(eventItem);
        }
    }
}