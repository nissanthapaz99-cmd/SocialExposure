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

        var contactIds = allMessages.Select(x => x.SenderId == currentUser.Id ? x.ReceiverId : x.SenderId).Distinct();
        var contactsQuery = _context.Users.Where(x => contactIds.Contains(x.Id) && x.IsActive);
        if (!string.IsNullOrWhiteSpace(search))
            contactsQuery = contactsQuery.Where(x => x.FullName.Contains(search) || x.Email.Contains(search));

        var contacts = await contactsQuery.OrderBy(x => x.FullName).ToListAsync();
        var conversations = contacts.Select(contact =>
        {
            var messages = allMessages.Where(x => x.SenderId == contact.Id || x.ReceiverId == contact.Id).ToList();
            return new ConversationViewModel
            {
                Contact = contact,
                LastMessage = messages.LastOrDefault(),
                UnreadCount = messages.Count(x => x.SenderId == contact.Id && x.ReceiverId == currentUser.Id && !x.IsRead)
            };
        }).OrderByDescending(x => x.LastMessage?.SentAt).ToList();

        var selectedContact = contactId.HasValue
            ? await _context.Users.FirstOrDefaultAsync(x => x.Id == contactId && x.IsActive)
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
        var receiverExists = await _context.Users.AnyAsync(x => x.Id == receiverId && x.IsActive);

        if (receiverExists && receiverId != currentUser.Id && !string.IsNullOrWhiteSpace(content))
        {
            _context.Messages.Add(new Message
            {
                SenderId = currentUser.Id,
                ReceiverId = receiverId,
                Content = content.Trim(),
                SentAt = DateTime.Now
            });
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index), new { contactId = receiverId });
    }

    private async Task<User> GetCurrentUser()
    {
        var userId = User.GetUserId();
        return await _context.Users.SingleAsync(x => x.Id == userId && x.IsActive);
    }
}
