using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ZuMarket.Models;

namespace ZuMarket.Hubs;

[Authorize]
public sealed class ChatHub : Hub
{
    private readonly ApplicationDbContext _db;

    public ChatHub(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task JoinConversation(int conversationId)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null || !await _db.Conversations.AnyAsync(x => x.Id == conversationId && (x.BuyerId == userId || x.SellerId == userId)))
            throw new HubException("ConversationAccessDenied");
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(conversationId));
    }

    public static string GroupName(int conversationId) => $"conversation-{conversationId}";
}
