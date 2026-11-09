using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ZuMarket.Hubs;
using ZuMarket.Models;

namespace ZuMarket.Controllers;

[Authorize]
public class MessagesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IHubContext<ChatHub> _hub;
    public MessagesController(ApplicationDbContext db, UserManager<ApplicationUser> users, IHubContext<ChatHub> hub) { _db = db; _users = users; _hub = hub; }

    public async Task<IActionResult> Index()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var conversations = await _db.Conversations.AsNoTracking()
            .Where(x => x.BuyerId == id || x.SellerId == id)
            .OrderByDescending(x => x.LastMessageAtUtc)
            .Select(x => new ConversationSummaryViewModel
            {
                Id = x.Id,
                ListingId = x.ListingId,
                ListingTitle = x.Listing!.Title,
                OtherParticipantName = x.BuyerId == id ? x.Seller!.FullName : x.Buyer!.FullName,
                LatestMessage = x.Messages.OrderByDescending(m => m.SentAtUtc).Select(m => m.Body).FirstOrDefault(),
                LastMessageAtUtc = x.LastMessageAtUtc,
                UnreadCount = x.Messages.Count(m => m.SenderId != id && m.ReadAtUtc == null)
            })
            .ToListAsync();
        return View(conversations);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int listingId)
    {
        var user = await _users.GetUserAsync(User);
        if (user?.CanUseMarketplace != true) return RedirectToAction("Index", "Verification");
        var listing = await _db.Listings.FirstOrDefaultAsync(x => x.Id == listingId && x.ModerationStatus == ListingModerationStatus.Approved && x.AvailabilityStatus == ListingAvailabilityStatus.Available);
        if (listing is null) return NotFound();
        if (listing.SellerId == user.Id) return RedirectToAction("Details", "Listings", new { id = listingId });
        var conversation = await _db.Conversations.FirstOrDefaultAsync(x => x.ListingId == listingId && x.BuyerId == user.Id && x.SellerId == listing.SellerId);
        if (conversation is null)
        {
            conversation = new Conversation { ListingId = listingId, BuyerId = user.Id, SellerId = listing.SellerId };
            _db.Conversations.Add(conversation); await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Details), new { id = conversation.Id });
    }

    public async Task<IActionResult> Details(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var conversation = await _db.Conversations.Include(x => x.Listing).Include(x => x.Buyer).Include(x => x.Seller).Include(x => x.Messages).ThenInclude(x => x.Sender)
            .FirstOrDefaultAsync(x => x.Id == id && (x.BuyerId == userId || x.SellerId == userId));
        if (conversation is null) return NotFound();
        foreach (var message in conversation.Messages.Where(x => x.SenderId != userId && x.ReadAtUtc == null)) message.ReadAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return View(conversation);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(int id, MessageViewModel model)
    {
        var user = await _users.GetUserAsync(User);
        var ajax = Request.Headers.XRequestedWith == "XMLHttpRequest";
        if (user?.CanUseMarketplace != true) return ajax ? StatusCode(StatusCodes.Status403Forbidden, new { error = "MarketplaceAccessRequired" }) : Forbid();
        var conversation = await _db.Conversations.FirstOrDefaultAsync(x => x.Id == id && (x.BuyerId == user.Id || x.SellerId == user.Id));
        if (conversation is null) return NotFound();
        var body = model.Body?.Trim();
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(body))
        {
            if (ajax) return BadRequest(new { error = "MessageRequired" });
            TempData["Error"] = "MessageRequired";
            return RedirectToAction(nameof(Details), new { id });
        }
        var rateLimitSince = DateTime.UtcNow.AddSeconds(-3);
        if (await _db.Messages.AnyAsync(x => x.ConversationId == id && x.SenderId == user.Id && x.SentAtUtc >= rateLimitSince))
        {
            if (ajax) return StatusCode(StatusCodes.Status429TooManyRequests, new { error = "MessageRateLimited" });
            TempData["Error"] = "MessageRateLimited";
            return RedirectToAction(nameof(Details), new { id });
        }
        var message = new Message { ConversationId = id, SenderId = user.Id, Body = body };
        _db.Messages.Add(message);
        conversation.LastMessageAtUtc = message.SentAtUtc;
        await _db.SaveChangesAsync();
        var payload = new { message.Id, message.ConversationId, message.SenderId, message.Body, message.SentAtUtc };
        await _hub.Clients.Group(ChatHub.GroupName(id)).SendAsync("ReceiveMessage", payload);
        if (ajax) return Json(payload);
        return RedirectToAction(nameof(Details), new { id });
    }
}
