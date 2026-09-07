using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialExposure.Data;
using SocialExposure.Models;
using SocialExposure.Services;

namespace SocialExposure.Controllers;

[Authorize(Roles = UserRoles.Admin)]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly NotificationService _notificationService;

    public AdminController(
        ApplicationDbContext context,
        IPasswordHasher<User> passwordHasher,
        NotificationService notificationService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _notificationService = notificationService;
    }

    public async Task<IActionResult> Dashboard()
    {
        var users = await _context.Users.AsNoTracking().ToListAsync();
        ViewBag.TotalUsers = users.Count;
        ViewBag.ActiveStaff = users.Count(x => x.Role == UserRoles.Staff && x.IsActive);
        ViewBag.ActiveClients = users.Count(x =>
            x.Role == UserRoles.Client && x.IsActive && x.IsApproved);
        ViewBag.PendingUsers = users.Count(x =>
            x.Role == UserRoles.Client && x.IsVerified && x.IsActive && !x.IsApproved);
        ViewBag.SuspendedUsers = users.Count(x => x.IsApproved && !x.IsActive);
        ViewBag.PendingApprovals = users
            .Where(x => x.Role == UserRoles.Client && x.IsVerified &&
                x.IsActive && !x.IsApproved)
            .OrderBy(x => x.FullName)
            .ToList();
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> UserManagement(string? search, string? role, string? status)
    {
        var query = _context.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(x => x.FullName.Contains(search) || x.Email.Contains(search));
        if (UserRoles.IsValid(role))
            query = query.Where(x => x.Role == role);
        query = status switch
        {
            "Active" => query.Where(x => x.IsActive && x.IsVerified && x.IsApproved),
            "Suspended" => query.Where(x => !x.IsActive && x.IsApproved),
            "Pending" => query.Where(x => x.IsVerified && x.IsActive && !x.IsApproved),
            "Unverified" => query.Where(x => !x.IsVerified && x.IsActive && !x.IsApproved),
            "Rejected" => query.Where(x => !x.IsActive && !x.IsApproved),
            _ => query
        };
        ViewBag.Search = search;
        ViewBag.Role = role;
        ViewBag.Status = status;
        return View(await query.OrderBy(x => x.FullName).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> StaffManagement() =>
        View(await _context.Users.AsNoTracking()
            .Where(x => x.Role == UserRoles.Staff || x.Role == UserRoles.Admin)
            .OrderBy(x => x.FullName).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> ClientManagement() =>
        View(await _context.Users.AsNoTracking()
            .Where(x => x.Role == UserRoles.Client)
            .OrderBy(x => x.FullName).ToListAsync());

    [HttpGet]
    public IActionResult ActivityLogs() => View();

    [HttpGet]
    public IActionResult CreateUser(string? role)
    {
        ViewBag.SelectedRole = UserRoles.IsValid(role) ? role : UserRoles.Client;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateUser(string fullName, string email, string role, string? temporaryPassword)
    {
        var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(fullName)) ModelState.AddModelError("fullName", "Full name is required.");
        if (string.IsNullOrWhiteSpace(email)) ModelState.AddModelError("email", "Email is required.");
        if (!UserRoles.IsValid(role)) ModelState.AddModelError("role", "Select a valid role.");
        if ((role is UserRoles.Admin or UserRoles.Staff) && string.IsNullOrWhiteSpace(temporaryPassword))
            ModelState.AddModelError("temporaryPassword", "A temporary password is required for staff and admins.");
        if (await _context.Users.AnyAsync(x => x.Email.ToLower() == normalizedEmail))
            ModelState.AddModelError("email", "That email address is already registered.");

        if (!ModelState.IsValid)
        {
            ViewBag.SelectedRole = role;
            ViewBag.FullName = fullName;
            ViewBag.Email = email;
            return View();
        }

        var user = new User
        {
            FullName = fullName.Trim(),
            Email = normalizedEmail,
            Role = role,
            IsActive = true,
            IsVerified = true,
            IsApproved = true
        };
        if (role is UserRoles.Admin or UserRoles.Staff)
            user.Password = _passwordHasher.HashPassword(user, temporaryPassword!);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        await _notificationService.QueueForUserAsync(
            user.Id,
            "Account created",
            $"Your {user.Role} account has been created and is ready to use.",
            "account",
            Url.Action("Profile", "Account"));
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(UserManagement));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id, string? returnTo)
    {
        var user = await _context.Users.FindAsync(id);
        var currentUserId = int.TryParse(
            User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedId)
            ? parsedId
            : 0;

        if (user != null && user.IsApproved && user.Id != currentUserId)
        {
            user.IsActive = !user.IsActive;

            await _notificationService.QueueForUserAsync(
                user.Id,
                user.IsActive ? "Account activated" : "Account suspended",
                user.IsActive
                    ? "Your Social Exposure account has been activated."
                    : "Your Social Exposure account has been suspended by an administrator.",
                "account",
                user.IsActive ? Url.Action("Profile", "Account") : null);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(ResolveReturnAction(returnTo));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveUser(int id, string? returnTo)
    {
        var user = await _context.Users.FirstOrDefaultAsync(x =>
            x.Id == id && x.Role == UserRoles.Client && x.IsVerified && !x.IsApproved);

        if (user != null)
        {
            user.IsApproved = true;
            user.IsActive = true;
            await _notificationService.QueueForUserAsync(
                user.Id,
                "Account approved",
                "Your Client account has been approved. You can now sign in.",
                "account",
                Url.Action("Login", "Account"));
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(ResolveReturnAction(returnTo));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectUser(int id, string? returnTo)
    {
        var user = await _context.Users.FirstOrDefaultAsync(x =>
            x.Id == id && x.Role == UserRoles.Client && !x.IsApproved);

        if (user != null)
        {
            user.IsActive = false;
            await _notificationService.QueueForUserAsync(
                user.Id,
                "Registration not approved",
                "Your Client registration was not approved. Contact an administrator if you need help.",
                "account");
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(ResolveReturnAction(returnTo));
    }

    private static string ResolveReturnAction(string? returnTo) =>
        returnTo is nameof(StaffManagement) or nameof(ClientManagement)
            ? returnTo
            : nameof(UserManagement);
}
