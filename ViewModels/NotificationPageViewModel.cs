using SocialExposure.Models;

namespace SocialExposure.ViewModels;

public class NotificationPageViewModel
{
    public User CurrentUser { get; set; } = new();
    public IReadOnlyList<Notification> Notifications { get; set; } = [];
    public string Filter { get; set; } = "all";
    public int UnreadCount { get; set; }
}
