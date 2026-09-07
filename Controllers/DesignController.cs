using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialExposure.Data;
using SocialExposure.Models;

namespace SocialExposure.Controllers
{
    [Authorize]
    public class DesignController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public DesignController(
            ApplicationDbContext context,
            IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
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


            // -----------------------------------------
            // STAFF / ADMIN
            // -----------------------------------------

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


            // -----------------------------------------
            // CLIENT
            // -----------------------------------------

            var email = User.FindFirstValue(
                ClaimTypes.Email);

            if (string.IsNullOrEmpty(email))
            {
                return Unauthorized();
            }

            var client = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.Email == email &&
                    u.Role == "Client");

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


            // -----------------------------------------
            // STAFF / ADMIN
            // -----------------------------------------

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


            // -----------------------------------------
            // CLIENT
            // -----------------------------------------

            var email = User.FindFirstValue(
                ClaimTypes.Email);

            if (string.IsNullOrEmpty(email))
            {
                return Unauthorized();
            }

            var client = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.Email == email &&
                    u.Role == "Client");

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
            // -----------------------------------------
            // BASIC VALIDATION
            // -----------------------------------------

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


            // -----------------------------------------
            // RETURN IF VALIDATION FAILED
            // -----------------------------------------

            if (!ModelState.IsValid)
            {
                ViewBag.DesignName = designName;
                ViewBag.Description = description;
                ViewBag.SelectedEventId = eventId;
                ViewBag.SelectedClientId = clientId;

                await LoadUploadData();

                return View();
            }


            // -----------------------------------------
            // CHECK EVENT
            // -----------------------------------------

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


            // -----------------------------------------
            // CHECK CLIENT
            // -----------------------------------------

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


            // -----------------------------------------
            // ALLOWED FILE TYPES
            // -----------------------------------------

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


            // -----------------------------------------
            // CREATE UPLOAD FOLDER
            // -----------------------------------------

            var uploadFolder = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                "designs");


            if (!Directory.Exists(uploadFolder))
            {
                Directory.CreateDirectory(uploadFolder);
            }


            // -----------------------------------------
            // UNIQUE FILE NAME
            // -----------------------------------------

            var storedFileName =
                $"{Guid.NewGuid()}{extension}";


            var physicalFilePath = Path.Combine(
                uploadFolder,
                storedFileName);


            // -----------------------------------------
            // SAVE PHYSICAL FILE
            // -----------------------------------------

            using (var stream = new FileStream(
                physicalFilePath,
                FileMode.Create))
            {
                await designFile.CopyToAsync(stream);
            }


            // -----------------------------------------
            // AUTOMATIC VERSION
            // -----------------------------------------

            var existingDesignCount =
                await _context.Designs
                    .CountAsync(d =>
                        d.EventId == selectedEvent.Id &&
                        d.ClientId == selectedClient.Id);


            var nextVersion =
                $"v{existingDesignCount + 1}.0";


            // -----------------------------------------
            // CREATE DESIGN
            // -----------------------------------------

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


            // -----------------------------------------
            // SUCCESS
            // -----------------------------------------

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


            // -----------------------------------------
            // VALIDATION
            // -----------------------------------------

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


            // -----------------------------------------
            // CHECK EVENT BELONGS TO CLIENT
            // -----------------------------------------

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


            // -----------------------------------------
            // RETURN IF INVALID
            // -----------------------------------------

            if (!ModelState.IsValid)
            {
                ViewBag.Events = await _context.Events
                    .Where(e =>
                        e.ClientEmail == client.Email)
                    .OrderBy(e => e.EventName)
                    .ToListAsync();

                return View();
            }


            // -----------------------------------------
            // ALLOWED FILE TYPES
            // -----------------------------------------

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


            // -----------------------------------------
            // CREATE UPLOAD FOLDER
            // -----------------------------------------

            var uploadFolder = Path.Combine(
                _environment.WebRootPath,
                "uploads",
                "designs");


            if (!Directory.Exists(uploadFolder))
            {
                Directory.CreateDirectory(uploadFolder);
            }


            // -----------------------------------------
            // UNIQUE FILE NAME
            // -----------------------------------------

            var storedFileName =
                $"{Guid.NewGuid()}{extension}";


            var physicalFilePath = Path.Combine(
                uploadFolder,
                storedFileName);


            // -----------------------------------------
            // SAVE FILE
            // -----------------------------------------

            using (var stream = new FileStream(
                physicalFilePath,
                FileMode.Create))
            {
                await designFile.CopyToAsync(stream);
            }


            // -----------------------------------------
            // AUTOMATIC VERSION
            // -----------------------------------------

            var existingDesignCount =
                await _context.Designs
                    .CountAsync(d =>
                        d.EventId == selectedEvent!.Id &&
                        d.ClientId == client.Id);


            var nextVersion =
                $"v{existingDesignCount + 1}.0";


            // -----------------------------------------
            // CREATE DESIGN RECORD
            // -----------------------------------------

            var design = new Design
            {
                FileName =
                    Path.GetFileName(
                        designFile.FileName),

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


            // -----------------------------------------
            // SUCCESS
            // -----------------------------------------

            TempData["SuccessMessage"] =
                "File uploaded successfully and sent for review.";


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
