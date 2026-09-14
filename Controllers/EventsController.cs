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
                    .Include(e => e.EventStaff)
                    .ThenInclude(es => es.Staff)
                    .OrderByDescending(e => e.Id)
                    .ToList();

                return View(events);
            }

            // Client only sees events assigned to their email
            var clientEmail = User.FindFirstValue(ClaimTypes.Email)?.ToLower();

            var clientEvents = _context.Events
                .Where(e => e.ClientEmail.ToLower() == clientEmail)
                .Include(e => e.EventStaff)
                .ThenInclude(es => es.Staff)
                .OrderByDescending(e => e.Id)
                .ToList();

            return View(clientEvents);
        }

        // =========================
        // CREATE EVENT - GET
        // =========================
        [HttpGet]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff)]
        public async Task<IActionResult> Create()
        {
            await LoadStaff();

            return View();
        }

        // =========================
        // CREATE EVENT - POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff)]
        public async Task<IActionResult> Create(
            Event model,
            int[]? staffIds)
        {
            if (!ModelState.IsValid)
            {
                await LoadStaff(staffIds);
                return View(model);
            }

            // Validate selected staff
            var selectedStaffIds = staffIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList()
                ?? new List<int>();

            if (selectedStaffIds.Any())
            {
                var validStaffIds = await _context.Users
                    .Where(u =>
                        selectedStaffIds.Contains(u.Id) &&
                        u.Role == UserRoles.Staff &&
                        u.IsActive)
                    .Select(u => u.Id)
                    .ToListAsync();

                if (validStaffIds.Count != selectedStaffIds.Count)
                {
                    ModelState.AddModelError(
                        "staffIds",
                        "One or more selected staff members are invalid.");

                    await LoadStaff(selectedStaffIds);
                    return View(model);
                }
            }

            model.Status = string.IsNullOrWhiteSpace(model.Status)
                ? "Pending"
                : model.Status;

            _context.Events.Add(model);
            await _context.SaveChangesAsync();

            // =========================
            // ASSIGN STAFF TO EVENT
            // =========================

            foreach (var staffId in selectedStaffIds)
            {
                _context.EventStaff.Add(new EventStaff
                {
                    EventId = model.Id,
                    StaffId = staffId
                });
            }

            await _context.SaveChangesAsync();

            // =========================
            // NOTIFY CLIENT
            // =========================

            await _notificationService.QueueForClientEmailAsync(
                model.ClientEmail,
                "New event assigned",
                $"{model.EventName} has been added to your account by {User.Identity?.Name ?? "the Social Exposure team"}.",
                "project",
                Url.Action(
                    nameof(Details),
                    "Events",
                    new { id = model.Id }));

            // =========================
            // NOTIFY ASSIGNED STAFF
            // =========================

            if (selectedStaffIds.Any())
            {
                var assignedStaff = await _context.Users
                    .Where(u =>
                        selectedStaffIds.Contains(u.Id) &&
                        u.Role == UserRoles.Staff &&
                        u.IsActive)
                    .ToListAsync();

                foreach (var staff in assignedStaff)
                {
                    await _notificationService.QueueForClientEmailAsync(
                        staff.Email,
                        "New event assigned",
                        $"You have been assigned to the event '{model.EventName}'.",
                        "project",
                        Url.Action(
                            nameof(Details),
                            "Events",
                            new { id = model.Id }));
                }
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ViewStaff));
        }

        // =========================
        // CLIENT EVENTS PAGE
        // =========================
        [Authorize(Roles = UserRoles.Client)]
        public IActionResult Client()
        {
            var clientEmail = User.FindFirstValue(
                ClaimTypes.Email)?.ToLower();

            var events = _context.Events
                .Where(e =>
                    e.ClientEmail.ToLower() == clientEmail)
                .Include(e => e.EventStaff)
                .ThenInclude(es => es.Staff)
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
            var clientEmail = User.FindFirstValue(
                ClaimTypes.Email)?.ToLower();

            var events = _context.Events
                .Where(e =>
                    e.ClientEmail.ToLower() == clientEmail)
                .Include(e => e.EventStaff)
                .ThenInclude(es => es.Staff)
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
                .Include(e => e.EventStaff)
                .ThenInclude(es => es.Staff)
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
                .Include(e => e.EventStaff)
                .ThenInclude(es => es.Staff)
                .FirstOrDefault(e => e.Id == id);

            if (eventItem == null)
            {
                return NotFound();
            }

            var role = User.FindFirstValue(ClaimTypes.Role);

            // Prevent a client from opening another client's event manually
            if (role == UserRoles.Client)
            {
                var clientEmail = User.FindFirstValue(
                    ClaimTypes.Email);

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
        public async Task<IActionResult> Edit(int id)
        {
            var eventItem = await _context.Events
                .Include(e => e.EventStaff)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (eventItem == null)
            {
                return NotFound();
            }

            var selectedStaffIds = eventItem.EventStaff
                .Select(es => es.StaffId)
                .ToList();

            await LoadStaff(selectedStaffIds);

            return View(eventItem);
        }

        // =========================
        // EDIT EVENT - POST
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff)]
        public async Task<IActionResult> Edit(
            Event model,
            int[]? staffIds)
        {
            if (!ModelState.IsValid)
            {
                await LoadStaff(staffIds);
                return View(model);
            }

            var eventItem = await _context.Events
                .Include(e => e.EventStaff)
                .FirstOrDefaultAsync(e => e.Id == model.Id);

            if (eventItem == null)
            {
                return NotFound();
            }

            // Validate selected staff
            var selectedStaffIds = staffIds?
                .Where(id => id > 0)
                .Distinct()
                .ToList()
                ?? new List<int>();

            if (selectedStaffIds.Any())
            {
                var validStaffIds = await _context.Users
                    .Where(u =>
                        selectedStaffIds.Contains(u.Id) &&
                        u.Role == UserRoles.Staff &&
                        u.IsActive)
                    .Select(u => u.Id)
                    .ToListAsync();

                if (validStaffIds.Count != selectedStaffIds.Count)
                {
                    ModelState.AddModelError(
                        "staffIds",
                        "One or more selected staff members are invalid.");

                    await LoadStaff(selectedStaffIds);
                    return View(model);
                }
            }

            var previousClientEmail = eventItem.ClientEmail;
            var previousStatus = eventItem.Status;

            // =========================
            // UPDATE EVENT INFORMATION
            // =========================

            eventItem.EventName = model.EventName;
            eventItem.ClientName = model.ClientName;
            eventItem.ClientEmail = model.ClientEmail;
            eventItem.Description = model.Description;
            eventItem.StartDate = model.StartDate;
            eventItem.Deadline = model.Deadline;
            eventItem.Status = model.Status;

            // =========================
            // UPDATE STAFF ASSIGNMENTS
            // =========================

            var existingAssignments =
                eventItem.EventStaff.ToList();

            foreach (var assignment in existingAssignments)
            {
                if (!selectedStaffIds.Contains(assignment.StaffId))
                {
                    _context.EventStaff.Remove(assignment);
                }
            }

            var existingStaffIds = existingAssignments
                .Select(es => es.StaffId)
                .ToHashSet();

            foreach (var staffId in selectedStaffIds)
            {
                if (!existingStaffIds.Contains(staffId))
                {
                    _context.EventStaff.Add(new EventStaff
                    {
                        EventId = eventItem.Id,
                        StaffId = staffId
                    });
                }
            }

            var link = Url.Action(
                nameof(Details),
                "Events",
                new { id = eventItem.Id });

            var clientChanged = !string.Equals(
                previousClientEmail,
                eventItem.ClientEmail,
                StringComparison.OrdinalIgnoreCase);

            // =========================
            // CLIENT NOTIFICATION
            // =========================

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
                    statusChanged
                        ? "Event status updated"
                        : "Event updated",
                    statusChanged
                        ? $"{eventItem.EventName} is now {eventItem.Status}."
                        : $"The details for {eventItem.EventName} were updated.",
                    "project",
                    link);
            }

            // =========================
            // SAVE CHANGES
            // =========================

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

            return RedirectToAction(
                "Upload",
                "Design",
                new { eventId = eventItem.Id });
        }

        // =========================
        // COMPLETE EVENT
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff)]
        public async Task<IActionResult> Complete(int id)
        {
            var eventItem = await _context.Events
                .FirstOrDefaultAsync(e => e.Id == id);

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
                Url.Action(
                    nameof(Details),
                    "Events",
                    new { id = eventItem.Id }));

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(ViewStaff));
        }

        // =========================
        // LOAD ACTIVE STAFF
        // =========================
        private async Task LoadStaff(
            IEnumerable<int>? selectedStaffIds = null)
        {
            var staff = await _context.Users
                .Where(u =>
                    u.Role == UserRoles.Staff &&
                    u.IsActive)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            ViewBag.Staff = staff;

            ViewBag.SelectedStaffIds =
                selectedStaffIds?.ToList()
                ?? new List<int>();
        }
    }
}