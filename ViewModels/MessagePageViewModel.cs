using SocialExposure.Models;

namespace SocialExposure.ViewModels;

public class ConversationViewModel
{
    public User Contact { get; set; } = new();
    public Message? LastMessage { get; set; }
    public int UnreadCount { get; set; }
}

public class MessagePageViewModel
{
    public User CurrentUser { get; set; } = new();
    public User? SelectedContact { get; set; }
    public IReadOnlyList<ConversationViewModel> Conversations { get; set; } = [];
    public IReadOnlyList<Message> Messages { get; set; } = [];
    public string Search { get; set; } = string.Empty;
}
