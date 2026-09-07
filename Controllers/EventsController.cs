using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialExposure.Data;
using SocialExposure.Models;
using SocialExposure.Services;

namespace SocialExposure.Controllers
{
    [Authorize]
    public class EventsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly NotificationService _notificationService;

        public EventsController(
            ApplicationDbContext context,
            NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
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
            var clientEmail = User.FindFirstValue(ClaimTypes.Email)?.ToLower();

            var clientEvents = _context.Events
                .Where(e => e.ClientEmail.ToLower() == clientEmail)
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
        public async Task<IActionResult> Create(Event model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            model.Status = string.IsNullOrWhiteSpace(model.Status)
                ? "Pending"
                : model.Status;

            _context.Events.Add(model);
            await _context.SaveChangesAsync();

            await _notificationService.QueueForClientEmailAsync(
                model.ClientEmail,
                "New event assigned",
                $"{model.EventName} has been added to your account by {User.Identity?.Name ?? "the Social Exposure team"}.",
                "project",
                Url.Action(nameof(Details), "Events", new { id = model.Id }));
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ViewStaff));
        }

        // =========================
        // CLIENT EVENTS PAGE
        // =========================
        [Authorize(Roles = UserRoles.Client)]
        public IActionResult Client()
        {
            var clientEmail = User.FindFirstValue(ClaimTypes.Email)?.ToLower();

            var events = _context.Events
                .Where(e => e.ClientEmail.ToLower() == clientEmail)
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
            var clientEmail = User.FindFirstValue(ClaimTypes.Email)?.ToLower();

            var events = _context.Events
                .Where(e => e.ClientEmail.ToLower() == clientEmail)
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
        public async Task<IActionResult> Edit(Event model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.Id == model.Id);

            if (eventItem == null)
            {
                return NotFound();
            }

            var previousClientEmail = eventItem.ClientEmail;
            var previousStatus = eventItem.Status;

            eventItem.EventName = model.EventName;
            eventItem.ClientName = model.ClientName;
            eventItem.ClientEmail = model.ClientEmail;
            eventItem.Description = model.Description;
            eventItem.StartDate = model.StartDate;
            eventItem.Deadline = model.Deadline;
            eventItem.Status = model.Status;

            var link = Url.Action(nameof(Details), "Events", new { id = eventItem.Id });
            var clientChanged = !string.Equals(
                previousClientEmail,
                eventItem.ClientEmail,
                StringComparison.OrdinalIgnoreCase);

            if (clientChanged)
            {
                await _notificationService.QueueForClientEmailAsync(
                    previousClientEmail,
                    "Event reassigned",
                    $"{eventItem.EventName} is no longer assigned to your account.",
                    "project");
                await _notificationService.QueueForClientEmailAsync(
                    eventItem.ClientEmail,
                    "New event assigned",
                    $"{eventItem.EventName} has been assigned to your account.",
                    "project",
                    link);
            }
            else
            {
                var statusChanged = !string.Equals(
                    previousStatus,
                    eventItem.Status,
                    StringComparison.OrdinalIgnoreCase);
                await _notificationService.QueueForClientEmailAsync(
                    eventItem.ClientEmail,
                    statusChanged ? "Event status updated" : "Event updated",
                    statusChanged
                        ? $"{eventItem.EventName} is now {eventItem.Status}."
                        : $"The details for {eventItem.EventName} were updated.",
                    "project",
                    link);
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ViewStaff));
        }

        // =========================
        // DELETE EVENT
        // =========================
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff)]
        public async Task<IActionResult> Delete(int id)
        {
            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.Id == id);

            if (eventItem == null)
            {
                return NotFound();
            }

            await _notificationService.QueueForClientEmailAsync(
                eventItem.ClientEmail,
                "Event removed",
                $"{eventItem.EventName} was removed from your account.",
                "project");
            _context.Events.Remove(eventItem);
            await _context.SaveChangesAsync();

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

            return RedirectToAction("Upload", "Design", new { eventId = eventItem.Id });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff)]
        public async Task<IActionResult> Complete(int id)
        {
            var eventItem = await _context.Events.FirstOrDefaultAsync(e => e.Id == id);

            if (eventItem == null)
            {
                return NotFound();
            }

            eventItem.Status = "Completed";

            await _notificationService.QueueForClientEmailAsync(
                eventItem.ClientEmail,
                "Event completed",
                $"{eventItem.EventName} has been marked as completed.",
                "project",
                Url.Action(nameof(Details), "Events", new { id = eventItem.Id }));
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ViewStaff));
        }
    }
}
