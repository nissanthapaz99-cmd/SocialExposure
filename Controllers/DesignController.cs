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
    public class DesignController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly NotificationService _notificationService;

        public DesignController(
            ApplicationDbContext context,
            IWebHostEnvironment environment,
            NotificationService notificationService)
        {
            _context = context;
            _environment = environment;
            _notificationService = notificationService;
        }

        // =========================================
        // DESIGN LIST
        // =========================================

        [HttpGet]
        public async Task<IActionResult> Index(
            string? search,
            string? status)
        {
            ViewBag.Search = search;
            ViewBag.Status = status;

            ViewBag.Statuses = new List<string>
            {
                "Pending Review",
                "Approved",
                "Changes Requested",
                "Rejected"
            };

            IQueryable<Design> designs = _context.Designs
                .Include(d => d.Event)
                .Include(d => d.Client);

            // =========================================
            // STAFF / ADMIN
            // =========================================

            if (User.IsInRole("Staff") ||
                User.IsInRole("Admin"))
            {
                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.Trim();

                    designs = designs.Where(d =>
                        d.FileName.Contains(search) ||
                        (d.Description != null &&
                         d.Description.Contains(search)) ||
                        (d.Event != null &&
                         d.Event.EventName.Contains(search)) ||
                        (d.Client != null &&
                         d.Client.FullName.Contains(search))
                    );
                }

                if (!string.IsNullOrWhiteSpace(status) &&
                    status != "All")
                {
                    designs = designs.Where(d =>
                        d.Status == status);
                }

                var allDesigns = await designs
                    .OrderByDescending(d => d.UploadedAt)
                    .ToListAsync();

                return View(allDesigns);
            }

            // =========================================
            // CLIENT - ONLY THEIR OWN DESIGNS
            // =========================================

            var email = User.FindFirstValue(
                ClaimTypes.Email);

            if (string.IsNullOrEmpty(email))
            {
                return Unauthorized();
            }

            var client = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.Email == email &&
                    u.Role == "Client" &&
                    u.IsActive);

            if (client == null)
            {
                return Unauthorized();
            }

            designs = designs.Where(d =>
                d.ClientId == client.Id);

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                designs = designs.Where(d =>
                    d.FileName.Contains(search) ||
                    (d.Description != null &&
                     d.Description.Contains(search)) ||
                    (d.Event != null &&
                     d.Event.EventName.Contains(search))
                );
            }

            if (!string.IsNullOrWhiteSpace(status) &&
                status != "All")
            {
                designs = designs.Where(d =>
                    d.Status == status);
            }

            var clientDesigns = await designs
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            return View(clientDesigns);
        }

        // =========================================
        // VIEW ALL DESIGNS
        // =========================================

        [HttpGet]
        public async Task<IActionResult> All(
            string? search,
            string? status)
        {
            ViewBag.Search = search;
            ViewBag.Status = status;

            ViewBag.Statuses = new List<string>
            {
                "Pending Review",
                "Approved",
                "Changes Requested",
                "Rejected"
            };

            IQueryable<Design> designs = _context.Designs
                .Include(d => d.Event)
                .Include(d => d.Client);

            // =========================================
            // STAFF / ADMIN
            // =========================================

            if (User.IsInRole("Staff") ||
                User.IsInRole("Admin"))
            {
                if (!string.IsNullOrWhiteSpace(search))
                {
                    search = search.Trim();

                    designs = designs.Where(d =>
                        d.FileName.Contains(search) ||
                        (d.Description != null &&
                         d.Description.Contains(search)) ||
                        (d.Event != null &&
                         d.Event.EventName.Contains(search)) ||
                        (d.Client != null &&
                         d.Client.FullName.Contains(search))
                    );
                }

                if (!string.IsNullOrWhiteSpace(status) &&
                    status != "All")
                {
                    designs = designs.Where(d =>
                        d.Status == status);
                }

                var allDesigns = await designs
                    .OrderByDescending(d => d.UploadedAt)
                    .ToListAsync();

                return View(allDesigns);
            }

            // =========================================
            // CLIENT
            // =========================================

            var email = User.FindFirstValue(
                ClaimTypes.Email);

            if (string.IsNullOrEmpty(email))
            {
                return Unauthorized();
            }

            var client = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.Email == email &&
                    u.Role == "Client" &&
                    u.IsActive);

            if (client == null)
            {
                return Unauthorized();
            }

            designs = designs.Where(d =>
                d.ClientId == client.Id);

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                designs = designs.Where(d =>
                    d.FileName.Contains(search) ||
                    (d.Description != null &&
                     d.Description.Contains(search)) ||
                    (d.Event != null &&
                     d.Event.EventName.Contains(search))
                );
            }

            if (!string.IsNullOrWhiteSpace(status) &&
                status != "All")
            {
                designs = designs.Where(d =>
                    d.Status == status);
            }

            var clientDesigns = await designs
                .OrderByDescending(d => d.UploadedAt)
                .ToListAsync();

            return View(clientDesigns);
        }

        // =========================================
        // STAFF / ADMIN UPLOAD PAGE
        // =========================================

        [HttpGet]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> Upload()
        {
            await LoadUploadData();

            return View();
        }

        // =========================================
        // STAFF / ADMIN UPLOAD DESIGN
        // =========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Staff,Admin")]
        public async Task<IActionResult> Upload(
            string designName,
            int eventId,
            int clientId,
            string? description,
            IFormFile? designFile)
        {
            if (string.IsNullOrWhiteSpace(designName))
            {
                ModelState.AddModelError(
                    "designName",
                    "Please enter a design name.");
            }

            if (eventId <= 0)
            {
                ModelState.AddModelError(
                    "eventId",
                    "Please select an event.");
            }

            if (clientId <= 0)
            {
                ModelState.AddModelError(
                    "clientId",
                    "Please select a client.");
            }

            if (designFile == null ||
                designFile.Length == 0)
            {
                ModelState.AddModelError(
                    "designFile",
                    "Please select a design file.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.DesignName = designName;
                ViewBag.Description = description;
                ViewBag.SelectedEventId = eventId;
                ViewBag.SelectedClientId = clientId;

                await LoadUploadData();

                return View();
            }

            var selectedEvent = await _context.Events
                .FirstOrDefaultAsync(e =>
                    e.Id == eventId);

            if (selectedEvent == null)
            {
                ModelState.AddModelError(
                    "eventId",
                    "Selected event was not found.");

                ViewBag.DesignName = designName;
                ViewBag.Description = description;
                ViewBag.SelectedEventId = eventId;
                ViewBag.SelectedClientId = clientId;

                await LoadUploadData();

                return View();
            }

            var selectedClient = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.Id == clientId &&
                    u.Role == "Client" &&
                    u.IsActive);

            if (selectedClient == null)
            {
                ModelState.AddModelError(
                    "clientId",
                    "Selected client was not found.");

                ViewBag.DesignName = designName;
                ViewBag.Description = description;
                ViewBag.SelectedEventId = eventId;
                ViewBag.SelectedClientId = clientId;

                await LoadUploadData();

                return View();
            }

            var allowedExtensions = new[]
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".pdf",
                ".zip",
                ".psd",
                ".ai",
                ".fig",
                ".xd"
            };

            var extension = Path
                .GetExtension(designFile!.FileName)
                .ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError(
                    "designFile",
                    "Unsupported file type. Please upload JPG, PNG, PDF, ZIP, PSD, AI, FIG or XD.");

                ViewBag.DesignName = designName;
                ViewBag.Description = description;
                ViewBag.SelectedEventId = eventId;
                ViewBag.SelectedClientId = clientId;

                await LoadUploadData();

                return View();
            }

            var uploadFolder = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                "designs");

            if (!Directory.Exists(uploadFolder))
            {
                Directory.CreateDirectory(uploadFolder);
            }

            var storedFileName =
                $"{Guid.NewGuid()}{extension}";

            var physicalFilePath = Path.Combine(
                uploadFolder,
                storedFileName);

            using (var stream = new FileStream(
                physicalFilePath,
                FileMode.Create))
            {
                await designFile.CopyToAsync(stream);
            }

            var existingDesignCount =
                await _context.Designs
                    .CountAsync(d =>
                        d.EventId == selectedEvent.Id &&
                        d.ClientId == selectedClient.Id);

            var nextVersion =
                $"v{existingDesignCount + 1}.0";

            var design = new Design
            {
                FileName =
                    designName.Trim(),

                FilePath =
                    $"/uploads/designs/{storedFileName}",

                Description =
                    description?.Trim(),

                Version =
                    nextVersion,

                UploadedAt =
                    DateTime.Now,

                EventId =
                    selectedEvent.Id,

                ClientId =
                    selectedClient.Id,

                Status =
                    "Pending Review"
            };

            _context.Designs.Add(design);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Design uploaded successfully as {nextVersion} and is now pending review.";

            return RedirectToAction(
                nameof(Index));
        }

        // =========================================
        // CLIENT UPLOAD PAGE
        // =========================================

        [HttpGet]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> ClientUpload()
        {
            var email = User.FindFirstValue(
                ClaimTypes.Email);

            if (string.IsNullOrEmpty(email))
            {
                return Unauthorized();
            }

            var client = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.Email == email &&
                    u.Role == "Client" &&
                    u.IsActive);

            if (client == null)
            {
                return Unauthorized();
            }

            var events = await _context.Events
                .Where(e =>
                    e.ClientEmail == client.Email)
                .OrderBy(e => e.EventName)
                .ToListAsync();

            ViewBag.Events = events;

            return View();
        }

        // =========================================
        // CLIENT UPLOAD FILE
        // =========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> ClientUpload(
            int eventId,
            string? description,
            IFormFile? designFile)
        {
            var email = User.FindFirstValue(
                ClaimTypes.Email);

            if (string.IsNullOrEmpty(email))
            {
                return Unauthorized();
            }

            var client = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.Email == email &&
                    u.Role == "Client" &&
                    u.IsActive);

            if (client == null)
            {
                return Unauthorized();
            }

            if (eventId <= 0)
            {
                ModelState.AddModelError(
                    "eventId",
                    "Please select an event.");
            }

            if (designFile == null ||
                designFile.Length == 0)
            {
                ModelState.AddModelError(
                    "designFile",
                    "Please select a file.");
            }

            var selectedEvent = await _context.Events
                .FirstOrDefaultAsync(e =>
                    e.Id == eventId &&
                    e.ClientEmail == client.Email);

            if (selectedEvent == null)
            {
                ModelState.AddModelError(
                    "eventId",
                    "Selected event was not found.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Events = await _context.Events
                    .Where(e =>
                        e.ClientEmail == client.Email)
                    .OrderBy(e => e.EventName)
                    .ToListAsync();

                return View();
            }

            var allowedExtensions = new[]
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".pdf",
                ".zip",
                ".psd",
                ".ai",
                ".fig",
                ".xd"
            };

            var extension = Path
                .GetExtension(designFile!.FileName)
                .ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError(
                    "designFile",
                    "Unsupported file type. Please upload JPG, PNG, PDF, ZIP, PSD, AI, FIG or XD.");

                ViewBag.Events = await _context.Events
                    .Where(e =>
                        e.ClientEmail == client.Email)
                    .OrderBy(e => e.EventName)
                    .ToListAsync();

                return View();
            }

            var uploadFolder = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                "designs");

            if (!Directory.Exists(uploadFolder))
            {
                Directory.CreateDirectory(uploadFolder);
            }

            var storedFileName =
                $"{Guid.NewGuid()}{extension}";

            var physicalFilePath = Path.Combine(
                uploadFolder,
                storedFileName);

            using (var stream = new FileStream(
                physicalFilePath,
                FileMode.Create))
            {
                await designFile.CopyToAsync(stream);
            }

            var existingDesignCount =
                await _context.Designs
                    .CountAsync(d =>
                        d.EventId == selectedEvent!.Id &&
                        d.ClientId == client.Id);

            var nextVersion =
                $"v{existingDesignCount + 1}.0";

            var originalFileName =
                Path.GetFileName(
                    designFile.FileName);

            var design = new Design
            {
                FileName =
                    originalFileName,

                FilePath =
                    $"/uploads/designs/{storedFileName}",

                Description =
                    description?.Trim(),

                Version =
                    nextVersion,

                UploadedAt =
                    DateTime.Now,

                EventId =
                    selectedEvent!.Id,

                ClientId =
                    client.Id,

                Status =
                    "Pending Review"
            };

            _context.Designs.Add(design);

            await _context.SaveChangesAsync();

            // =========================================
            // AUTOMATICALLY NOTIFY ASSIGNED STAFF
            // =========================================

            var notificationLink =
                Url.Action(
                    nameof(Index),
                    "Design");

            await _notificationService.QueueForEventStaffAsync(
                selectedEvent.Id,
                "New client file uploaded",
                $"{client.FullName} uploaded '{originalFileName}' for the event '{selectedEvent.EventName}'. The file is now pending review.",
                "design",
                notificationLink);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                "File uploaded successfully and the assigned staff have been notified.";

            return RedirectToAction(
                nameof(Index));
        }

        // =========================================
        // CLIENT EDIT DESIGN - GET
        // =========================================

        [HttpGet]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> Edit(int id)
        {
            var email = User.FindFirstValue(
                ClaimTypes.Email);

            if (string.IsNullOrEmpty(email))
            {
                return Unauthorized();
            }

            var client = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.Email == email &&
                    u.Role == "Client" &&
                    u.IsActive);

            if (client == null)
            {
                return Unauthorized();
            }

            var design = await _context.Designs
                .Include(d => d.Event)
                .Include(d => d.Client)
                .FirstOrDefaultAsync(d =>
                    d.Id == id &&
                    d.ClientId == client.Id);

            if (design == null)
            {
                return NotFound();
            }

            return View(design);
        }

        // =========================================
        // CLIENT EDIT DESIGN - POST
        // =========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> Edit(
            int id,
            string submissionType,
            string? description,
            IFormFile? designFile)
        {
            var email = User.FindFirstValue(
                ClaimTypes.Email);

            if (string.IsNullOrEmpty(email))
            {
                return Unauthorized();
            }

            var client = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.Email == email &&
                    u.Role == "Client" &&
                    u.IsActive);

            if (client == null)
            {
                return Unauthorized();
            }

            // =========================================
            // FIND ORIGINAL DESIGN
            // =========================================

            var existingDesign = await _context.Designs
                .Include(d => d.Event)
                .FirstOrDefaultAsync(d =>
                    d.Id == id &&
                    d.ClientId == client.Id);

            if (existingDesign == null)
            {
                return NotFound();
            }

            // =========================================
            // VALIDATE SUBMISSION TYPE
            // =========================================

            if (submissionType != "Revised" &&
                submissionType != "Reference")
            {
                submissionType = "Revised";
            }

            // =========================================
            // VALIDATE FILE
            // =========================================

            if (designFile == null ||
                designFile.Length == 0)
            {
                ModelState.AddModelError(
                    "designFile",
                    "Please select a file.");
            }

            if (!ModelState.IsValid)
            {
                return View(existingDesign);
            }

            // =========================================
            // ALLOWED FILE TYPES
            // =========================================

            var allowedExtensions = new[]
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".pdf",
                ".zip",
                ".psd",
                ".ai",
                ".fig",
                ".xd"
            };

            var extension = Path
                .GetExtension(designFile!.FileName)
                .ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError(
                    "designFile",
                    "Unsupported file type. Please upload JPG, PNG, PDF, ZIP, PSD, AI, FIG or XD.");

                return View(existingDesign);
            }

            // =========================================
            // UPLOAD FOLDER
            // =========================================

            var uploadFolder = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                "designs");

            if (!Directory.Exists(uploadFolder))
            {
                Directory.CreateDirectory(uploadFolder);
            }

            // =========================================
            // CREATE SAFE FILE NAME
            // =========================================

            var storedFileName =
                $"{Guid.NewGuid()}{extension}";

            var physicalFilePath = Path.Combine(
                uploadFolder,
                storedFileName);

            using (var stream = new FileStream(
                physicalFilePath,
                FileMode.Create))
            {
                await designFile.CopyToAsync(stream);
            }

            // =========================================
            // CALCULATE NEXT VERSION
            // =========================================

            var existingDesignCount =
                await _context.Designs
                    .CountAsync(d =>
                        d.EventId == existingDesign.EventId &&
                        d.ClientId == client.Id);

            var nextVersion =
                $"v{existingDesignCount + 1}.0";

            // =========================================
            // ORIGINAL FILE NAME
            // =========================================

            var originalFileName =
                Path.GetFileName(
                    designFile.FileName);

            // =========================================
            // DESCRIPTION
            // =========================================

            string submissionDescription;

            if (submissionType == "Reference")
            {
                submissionDescription =
                    string.IsNullOrWhiteSpace(description)
                        ? "Reference file submitted by client."
                        : $"Reference file: {description.Trim()}";
            }
            else
            {
                submissionDescription =
                    string.IsNullOrWhiteSpace(description)
                        ? "Revised design submitted by client."
                        : $"Revised design: {description.Trim()}";
            }

            // =========================================
            // CREATE NEW DESIGN VERSION
            // =========================================

            var newDesign = new Design
            {
                FileName =
                    originalFileName,

                FilePath =
                    $"/uploads/designs/{storedFileName}",

                Description =
                    submissionDescription,

                Version =
                    nextVersion,

                UploadedAt =
                    DateTime.Now,

                EventId =
                    existingDesign.EventId,

                ClientId =
                    client.Id,

                Status =
                    "Pending Review"
            };

            _context.Designs.Add(newDesign);

            await _context.SaveChangesAsync();

            // =========================================
            // AUTOMATIC STAFF NOTIFICATION
            // =========================================

            var notificationLink =
                Url.Action(
                    nameof(Index),
                    "Design");

            var notificationTitle =
                submissionType == "Reference"
                    ? "New reference file uploaded"
                    : "New revised design uploaded";

            var eventName =
                existingDesign.Event?.EventName
                ?? "the project";

            var notificationMessage =
                submissionType == "Reference"
                    ? $"{client.FullName} uploaded the reference file '{originalFileName}' for the event '{eventName}'."
                    : $"{client.FullName} uploaded the revised design '{originalFileName}' for the event '{eventName}'. It is now pending review.";

            await _notificationService.QueueForEventStaffAsync(
                existingDesign.EventId,
                notificationTitle,
                notificationMessage,
                "design",
                notificationLink);

            await _context.SaveChangesAsync();

            // =========================================
            // SUCCESS MESSAGE
            // =========================================

            TempData["SuccessMessage"] =
                submissionType == "Reference"
                    ? "Reference file uploaded successfully and the assigned staff have been notified."
                    : $"Revised design uploaded successfully as {nextVersion} and the assigned staff have been notified.";

            return RedirectToAction(
                nameof(Index));
        }

        // =========================================
        // CLIENT APPROVE DESIGN
        // =========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> Approve(int id)
        {
            var email = User.FindFirstValue(
                ClaimTypes.Email);

            if (string.IsNullOrEmpty(email))
            {
                return Unauthorized();
            }

            var client = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.Email == email &&
                    u.Role == "Client" &&
                    u.IsActive);

            if (client == null)
            {
                return Unauthorized();
            }

            var design = await _context.Designs
                .Include(d => d.Event)
                .FirstOrDefaultAsync(d =>
                    d.Id == id &&
                    d.ClientId == client.Id);

            if (design == null)
            {
                return NotFound();
            }

            design.Status = "Approved";

            await _context.SaveChangesAsync();

            // =========================================
            // NOTIFY ASSIGNED STAFF
            // =========================================

            if (design.Event != null)
            {
                var eventName =
                    design.Event.EventName;

                await _notificationService.QueueForEventStaffAsync(
                    design.EventId,
                    "Design approved",
                    $"{client.FullName} approved the design '{design.FileName}' for the event '{eventName}'.",
                    "design",
                    Url.Action(
                        nameof(Index),
                        "Design"));

                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] =
                "Design approved successfully.";

            return RedirectToAction(
                nameof(Index));
        }

        // =========================================
        // CLIENT NOT APPROVED DESIGN
        // =========================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Client")]
        public async Task<IActionResult> NotApproved(int id)
        {
            var email = User.FindFirstValue(
                ClaimTypes.Email);

            if (string.IsNullOrEmpty(email))
            {
                return Unauthorized();
            }

            var client = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.Email == email &&
                    u.Role == "Client" &&
                    u.IsActive);

            if (client == null)
            {
                return Unauthorized();
            }

            var design = await _context.Designs
                .Include(d => d.Event)
                .FirstOrDefaultAsync(d =>
                    d.Id == id &&
                    d.ClientId == client.Id);

            if (design == null)
            {
                return NotFound();
            }

            design.Status = "Rejected";

            await _context.SaveChangesAsync();

            // =========================================
            // NOTIFY ASSIGNED STAFF
            // =========================================

            if (design.Event != null)
            {
                var eventName =
                    design.Event.EventName;

                await _notificationService.QueueForEventStaffAsync(
                    design.EventId,
                    "Design not approved",
                    $"{client.FullName} did not approve the design '{design.FileName}' for the event '{eventName}'. Please review the design.",
                    "design",
                    Url.Action(
                        nameof(Index),
                        "Design"));

                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] =
                "Design marked as not approved.";

            return RedirectToAction(
                nameof(Index));
        }

        // =========================================
        // LOAD STAFF / ADMIN UPLOAD DATA
        // =========================================

        private async Task LoadUploadData()
        {
            var events = await _context.Events
                .OrderBy(e => e.EventName)
                .ToListAsync();

            var clients = await _context.Users
                .Where(u =>
                    u.Role == "Client" &&
                    u.IsActive)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            ViewBag.Events = events;
            ViewBag.Clients = clients;
        }
    }
}