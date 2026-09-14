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

    // =========================================
    // QUEUE NOTIFICATION FOR ONE USER
    // =========================================

    public async Task<bool> QueueForUserAsync(
        int userId,
        string title,
        string message,
        string type,
        string? link = null)
    {
        if (!await _context.Users.AnyAsync(x => x.Id == userId))
        {
            return false;
        }

        Queue(
            userId,
            title,
            message,
            type,
            link);

        return true;
    }

    // =========================================
    // QUEUE NOTIFICATION FOR CLIENT BY EMAIL
    // =========================================

    public async Task<bool> QueueForClientEmailAsync(
        string? email,
        string title,
        string message,
        string type,
        string? link = null)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var normalizedEmail =
            email.Trim().ToLowerInvariant();

        var userId = await _context.Users
            .Where(x =>
                x.Role == UserRoles.Client &&
                x.Email.ToLower() == normalizedEmail)
            .Select(x => (int?)x.Id)
            .FirstOrDefaultAsync();

        if (!userId.HasValue)
        {
            return false;
        }

        Queue(
            userId.Value,
            title,
            message,
            type,
            link);

        return true;
    }

    // =========================================
    // QUEUE NOTIFICATION FOR ALL USERS IN ROLE
    // =========================================

    public async Task<int> QueueForRoleAsync(
        string role,
        string title,
        string message,
        string type,
        string? link = null,
        int? exceptUserId = null)
    {
        var userIds = await _context.Users
            .Where(x =>
                x.Role == role &&
                x.IsActive &&
                x.IsApproved &&
                (!exceptUserId.HasValue ||
                 x.Id != exceptUserId.Value))
            .Select(x => x.Id)
            .ToListAsync();

        foreach (var userId in userIds)
        {
            Queue(
                userId,
                title,
                message,
                type,
                link);
        }

        return userIds.Count;
    }

    // =========================================
    // QUEUE NOTIFICATION FOR EVENT STAFF
    // =========================================

    public async Task<int> QueueForEventStaffAsync(
        int eventId,
        string title,
        string message,
        string type,
        string? link = null)
    {
        var staffIds = await _context.EventStaff
            .Where(es =>
                es.EventId == eventId &&
                es.Staff != null &&
                es.Staff.IsActive &&
                es.Staff.IsApproved &&
                es.Staff.Role == UserRoles.Staff)
            .Select(es => es.StaffId)
            .Distinct()
            .ToListAsync();

        foreach (var staffId in staffIds)
        {
            Queue(
                staffId,
                title,
                message,
                type,
                link);
        }

        return staffIds.Count;
    }

    // =========================================
    // ADD NOTIFICATION TO DATABASE
    // =========================================

    private void Queue(
        int userId,
        string title,
        string message,
        string type,
        string? link)
    {
        _context.Notifications.Add(
            new Notification
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