using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialExposure.Data;
using SocialExposure.Models;

namespace SocialExposure.Controllers;

[Authorize(Roles = UserRoles.Admin)]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IPasswordHasher<User> _passwordHasher;

    public AdminController(ApplicationDbContext context, IPasswordHasher<User> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    public async Task<IActionResult> Dashboard()
    {
        var users = await _context.Users.AsNoTracking().ToListAsync();
        ViewBag.TotalUsers = users.Count;
        ViewBag.ActiveStaff = users.Count(x => x.Role == UserRoles.Staff && x.IsActive);
        ViewBag.ActiveClients = users.Count(x => x.Role == UserRoles.Client && x.IsActive);
        ViewBag.PendingUsers = users.Count(x => !x.IsVerified);
        ViewBag.SuspendedUsers = users.Count(x => !x.IsActive);
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
            "Active" => query.Where(x => x.IsActive && x.IsVerified),
            "Suspended" => query.Where(x => !x.IsActive),
            "Pending" => query.Where(x => !x.IsVerified),
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
        if (string.IsNullOrWhiteSpace(fullName)) ModelState.AddModelError("fullName", "Full name is required.");
        if (string.IsNullOrWhiteSpace(email)) ModelState.AddModelError("email", "Email is required.");
        if (!UserRoles.IsValid(role)) ModelState.AddModelError("role", "Select a valid role.");
        if ((role is UserRoles.Admin or UserRoles.Staff) && string.IsNullOrWhiteSpace(temporaryPassword))
            ModelState.AddModelError("temporaryPassword", "A temporary password is required for staff and admins.");
        if (await _context.Users.AnyAsync(x => x.Email == email))
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
            Email = email.Trim(),
            Role = role,
            IsActive = true,
            IsVerified = true
        };
        if (role is UserRoles.Admin or UserRoles.Staff)
            user.Password = _passwordHasher.HashPassword(user, temporaryPassword!);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(UserManagement));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id, string? returnTo)
    {
        var user = await _context.Users.FindAsync(id);
        if (user != null)
        {
            user.IsActive = !user.IsActive;
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(returnTo is nameof(StaffManagement) or nameof(ClientManagement) ? returnTo : nameof(UserManagement));
    }
}
