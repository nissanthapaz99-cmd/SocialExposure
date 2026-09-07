using Microsoft.EntityFrameworkCore;
using SocialExposure.Data;
using SocialExposure.Models;

namespace SocialExposure.Services;

public sealed class NotificationService
{
    private readonly ApplicationDbContext _context;

    public NotificationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> QueueForUserAsync(
        int userId,
        string title,
        string message,
        string type,
        string? link = null)
    {
        if (!await _context.Users.AnyAsync(x => x.Id == userId))
            return false;

        Queue(userId, title, message, type, link);
        return true;
    }

    public async Task<bool> QueueForClientEmailAsync(
        string? email,
        string title,
        string message,
        string type,
        string? link = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            return false;

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var userId = await _context.Users
            .Where(x => x.Role == UserRoles.Client && x.Email.ToLower() == normalizedEmail)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        if (!userId.HasValue)
            return false;

        Queue(userId.Value, title, message, type, link);
        return true;
    }

    public async Task<int> QueueForRoleAsync(
        string role,
        string title,
        string message,
        string type,
        string? link = null,
        int? exceptUserId = null)
    {
        var userIds = await _context.Users
            .Where(x => x.Role == role && x.IsActive && x.IsApproved &&
                (!exceptUserId.HasValue || x.Id != exceptUserId.Value))
            .Select(x => x.Id)
            .ToListAsync();

        foreach (var userId in userIds)
            Queue(userId, title, message, type, link);

        return userIds.Count;
    }

    private void Queue(int userId, string title, string message, string type, string? link)
    {
        _context.Notifications.Add(new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            Link = link,
            CreatedAt = DateTime.Now
        });
    }
}
