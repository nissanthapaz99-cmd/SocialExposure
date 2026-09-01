using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SocialExposure.Data;
using SocialExposure.Extensions;
using SocialExposure.Models;
using SocialExposure.ViewModels;

namespace SocialExposure.Controllers;

[Authorize]
public class MessageController : Controller
{
    private readonly ApplicationDbContext _context;

    public MessageController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    public async Task<IActionResult> Index(int? contactId = null, string search = "")
    {
        var currentUser = await GetCurrentUser();

        var allMessages = await _context.Messages
            .Include(x => x.Sender).Include(x => x.Receiver)
            .Where(x => x.SenderId == currentUser.Id || x.ReceiverId == currentUser.Id)
            .OrderBy(x => x.SentAt).ToListAsync();

        // Show every active user so a new conversation can be started before any
        // messages exist between the two accounts.
        var contactsQuery = _context.Users
            .Where(x => x.Id != currentUser.Id && x.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
            contactsQuery = contactsQuery.Where(x => x.FullName.Contains(search) || x.Email.Contains(search));

        var contacts = await contactsQuery.OrderBy(x => x.FullName).ToListAsync();
        var conversations = contacts.Select(contact =>
        {
            var messages = allMessages.Where(x =>
                (x.SenderId == currentUser.Id && x.ReceiverId == contact.Id) ||
                (x.SenderId == contact.Id && x.ReceiverId == currentUser.Id)).ToList();

            return new ConversationViewModel
            {
                Contact = contact,
                LastMessage = messages.LastOrDefault(),
                UnreadCount = messages.Count(x => x.SenderId == contact.Id && x.ReceiverId == currentUser.Id && !x.IsRead)
            };
        })
            .OrderByDescending(x => x.LastMessage?.SentAt ?? DateTime.MinValue)
            .ThenBy(x => x.Contact.FullName)
            .ToList();

        var selectedContact = contactId.HasValue
            ? await _context.Users.FirstOrDefaultAsync(x =>
                x.Id == contactId && x.Id != currentUser.Id && x.IsActive)
            : conversations.FirstOrDefault()?.Contact;

        var thread = selectedContact == null ? [] : allMessages
            .Where(x => (x.SenderId == currentUser.Id && x.ReceiverId == selectedContact.Id) ||
                        (x.SenderId == selectedContact.Id && x.ReceiverId == currentUser.Id)).ToList();

        var newlyRead = thread.Where(x => x.ReceiverId == currentUser.Id && !x.IsRead).ToList();
        if (newlyRead.Count > 0)
        {
            newlyRead.ForEach(x => x.IsRead = true);
            await _context.SaveChangesAsync();
        }

        return View(new MessagePageViewModel
        {
            CurrentUser = currentUser,
            SelectedContact = selectedContact,
            Conversations = conversations,
            Messages = thread,
            Search = search
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(int receiverId, string content)
    {
        var currentUser = await GetCurrentUser();
        var receiver = await _context.Users
            .FirstOrDefaultAsync(x => x.Id == receiverId && x.Id != currentUser.Id && x.IsActive);

        if (receiver == null)
        {
            TempData["MessageError"] = "That recipient is not available.";
            return RedirectToAction(nameof(Index));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            TempData["MessageError"] = "Enter a message before sending.";
            return RedirectToAction(nameof(Index), new { contactId = receiverId });
        }

        var messageText = content.Trim();
        if (messageText.Length > 2000)
            messageText = messageText[..2000];

        var sentAt = DateTime.Now;

        _context.Messages.Add(new Message
        {
            SenderId = currentUser.Id,
            ReceiverId = receiver.Id,
            Content = messageText,
            SentAt = sentAt
        });

        var preview = messageText.Length > 120 ? $"{messageText[..117]}..." : messageText;
        _context.Notifications.Add(new Notification
        {
            UserId = receiver.Id,
            Title = $"New message from {currentUser.FullName}",
            Message = preview,
            Type = "message",
            Link = Url.Action(nameof(Index), "Message", new { contactId = currentUser.Id }),
            CreatedAt = sentAt
        });

        await _context.SaveChangesAsync();
        TempData["MessageSuccess"] = $"Message sent to {receiver.FullName}.";

        return RedirectToAction(nameof(Index), new { contactId = receiverId });
    }

    private async Task<User> GetCurrentUser()
    {
        var userId = User.GetUserId();
        return await _context.Users.SingleAsync(x => x.Id == userId && x.IsActive);
    }
}


