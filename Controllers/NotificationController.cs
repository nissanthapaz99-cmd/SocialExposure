using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialExposure.Data;
using SocialExposure.Extensions;
using SocialExposure.ViewModels;

namespace SocialExposure.Controllers;

[Authorize]
public class NotificationController : Controller
{
    private readonly ApplicationDbContext _context;

    public NotificationController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Index(string filter = "all")
    {
        var currentUser = await GetCurrentUser();
        if (currentUser == null)
            return View(new NotificationPageViewModel { Filter = filter });

        var query = _context.Notifications.Where(x => x.UserId == currentUser.Id);
        var unreadCount = await query.CountAsync(x => !x.IsRead);

        if (filter.Equals("unread", StringComparison.OrdinalIgnoreCase))
            query = query.Where(x => !x.IsRead);

        return View(new NotificationPageViewModel
        {
            CurrentUser = currentUser,
            Notifications = await query.OrderByDescending(x => x.CreatedAt).ToListAsync(),
            Filter = filter,
            UnreadCount = unreadCount
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id, string filter = "all")
    {
        var currentUser = await GetCurrentUser();
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == currentUser.Id);

        if (notification != null)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index), new { filter });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var currentUser = await GetCurrentUser();
        var unread = await _context.Notifications
            .Where(x => x.UserId == currentUser.Id && !x.IsRead).ToListAsync();
        unread.ForEach(x => x.IsRead = true);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dismiss(int id, string filter = "all")
    {
        var currentUser = await GetCurrentUser();
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == currentUser.Id);

        if (notification != null)
        {
            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index), new { filter });
    }

    private async Task<SocialExposure.Models.User> GetCurrentUser()
    {
        var userId = User.GetUserId();
        return await _context.Users.SingleAsync(x => x.Id == userId && x.IsActive);
    }
}
