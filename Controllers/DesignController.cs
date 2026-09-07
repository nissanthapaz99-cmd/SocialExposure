using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialExposure.Data;
using SocialExposure.Extensions;
using SocialExposure.Models;
using SocialExposure.Services;
using SocialExposure.ViewModels;

namespace SocialExposure.Controllers;

[Authorize]
public class DesignController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly NotificationService _notificationService;
    private readonly string _uploadRoot;

    public DesignController(
        ApplicationDbContext context,
        IWebHostEnvironment environment,
        IConfiguration configuration,
        NotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
        var configuredRoot = configuration["DesignUploads:RootPath"];
        _uploadRoot = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(environment.ContentRootPath, "App_Data", "DesignUploads")
            : Path.GetFullPath(configuredRoot);
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var currentUser = await GetCurrentUserAsync();
        var query = _context.Designs
            .Include(x => x.Event)
            .Include(x => x.Client)
            .AsNoTracking();

        if (currentUser.Role == UserRoles.Client)
            query = query.Where(x => x.ClientId == currentUser.Id);

        return View(await query.OrderByDescending(x => x.UploadedAt).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Upload(int? eventId = null)
    {
        var model = new DesignUploadViewModel
        {
            EventId = eventId ?? 0,
            Events = await GetAvailableEventsAsync(await GetCurrentUserAsync())
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(DesignUploadPolicy.RequestMaxBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = DesignUploadPolicy.RequestMaxBytes)]
    public async Task<IActionResult> Upload(
        DesignUploadViewModel model,
        CancellationToken cancellationToken)
    {
        var currentUser = await GetCurrentUserAsync();
        var availableEvents = await GetAvailableEventsAsync(currentUser);
        var eventItem = availableEvents.FirstOrDefault(x => x.Id == model.EventId);

        if (eventItem == null)
            ModelState.AddModelError(nameof(model.EventId), "Select an event you are allowed to access.");

        var extension = string.Empty;
        var mediaKind = string.Empty;
        if (model.File != null &&
            !DesignUploadPolicy.TryValidateMetadata(
                model.File, out extension, out mediaKind, out var metadataError))
        {
            ModelState.AddModelError(nameof(model.File), metadataError);
        }

        if (model.File != null && ModelState.IsValid &&
            !await DesignUploadPolicy.HasValidSignatureAsync(
                model.File, extension, cancellationToken))
        {
            ModelState.AddModelError(
                nameof(model.File),
                "The selected file content is not a valid supported photo or video.");
        }

        User? client = null;
        if (eventItem != null)
        {
            var normalizedClientEmail = eventItem.ClientEmail.ToLower();
            client = await _context.Users.FirstOrDefaultAsync(x =>
                x.Role == UserRoles.Client && x.Email.ToLower() == normalizedClientEmail,
                cancellationToken);

            if (client == null)
            {
                ModelState.AddModelError(
                    nameof(model.EventId),
                    "The event must be assigned to a registered Client before files can be uploaded.");
            }
        }

        if (!ModelState.IsValid)
        {
            model.Events = availableEvents;
            return View(model);
        }

        var uploadRoot = GetUploadRoot();
        Directory.CreateDirectory(uploadRoot);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var storedFilePath = Path.Combine(uploadRoot, storedFileName);

        try
        {
            await using (var output = new FileStream(
                storedFilePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None))
            {
                await model.File!.CopyToAsync(output, cancellationToken);
            }

            var design = new Design
            {
                FileName = model.DesignName.Trim(),
                FilePath = storedFileName,
                Description = model.Description?.Trim(),
                Version = model.Version.Trim(),
                UploadedAt = DateTime.Now,
                EventId = eventItem!.Id,
                ClientId = client!.Id,
                Status = "Pending Review"
            };
            _context.Designs.Add(design);

            if (currentUser.Role == UserRoles.Client)
            {
                var notificationLink = Url.Action(nameof(Index), "Design");
                await _notificationService.QueueForRoleAsync(
                    UserRoles.Staff,
                    "Client uploaded a design",
                    $"{currentUser.FullName} uploaded {design.FileName} for {eventItem.EventName}.",
                    "project",
                    notificationLink,
                    currentUser.Id);
                await _notificationService.QueueForRoleAsync(
                    UserRoles.Admin,
                    "Client uploaded a design",
                    $"{currentUser.FullName} uploaded {design.FileName} for {eventItem.EventName}.",
                    "project",
                    notificationLink,
                    currentUser.Id);
            }
            else
            {
                await _notificationService.QueueForUserAsync(
                    client.Id,
                    "New design uploaded",
                    $"{design.FileName} was uploaded for {eventItem.EventName}.",
                    "project",
                    Url.Action(nameof(Index), "Design"));
            }

            await _context.SaveChangesAsync(cancellationToken);
            TempData["DesignSuccess"] = $"{char.ToUpperInvariant(mediaKind[0])}{mediaKind[1..]} uploaded successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch
        {
            if (System.IO.File.Exists(storedFilePath))
                System.IO.File.Delete(storedFilePath);
            throw;
        }
    }

    [HttpGet]
    public async Task<IActionResult> Download(int id)
    {
        var currentUser = await GetCurrentUserAsync();
        var design = await _context.Designs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id);

        if (design == null || string.IsNullOrWhiteSpace(design.FilePath))
            return NotFound();

        if (currentUser.Role == UserRoles.Client && design.ClientId != currentUser.Id)
            return Forbid();

        var storedFileName = Path.GetFileName(design.FilePath);
        if (!string.Equals(storedFileName, design.FilePath, StringComparison.Ordinal))
            return NotFound();

        var fullPath = Path.Combine(GetUploadRoot(), storedFileName);
        if (!System.IO.File.Exists(fullPath))
            return NotFound();

        return PhysicalFile(
            fullPath,
            DesignUploadPolicy.GetContentType(storedFileName),
            enableRangeProcessing: true);
    }

    private async Task<List<Event>> GetAvailableEventsAsync(User currentUser)
    {
        var query = _context.Events.AsNoTracking();
        if (currentUser.Role == UserRoles.Client)
        {
            var normalizedEmail = currentUser.Email.ToLower();
            query = query.Where(x => x.ClientEmail.ToLower() == normalizedEmail);
        }

        return await query.OrderByDescending(x => x.Id).ToListAsync();
    }

    private async Task<User> GetCurrentUserAsync()
    {
        var userId = User.GetUserId();
        return await _context.Users.SingleAsync(x =>
            x.Id == userId && x.IsActive && x.IsApproved);
    }

    private string GetUploadRoot() => _uploadRoot;
}
