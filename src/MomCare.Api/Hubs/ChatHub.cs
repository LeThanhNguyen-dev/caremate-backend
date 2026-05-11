using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MomCare.Data;

namespace MomCare.Api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly MomCareContext _context;

    public ChatHub(MomCareContext context)
    {
        _context = context;
    }

    public async Task JoinConversation(int conversationId)
    {
        var userId = GetUserId();
        var canAccess = await _context.Conversations.AnyAsync(c =>
            c.Id == conversationId &&
            (c.User1Id == userId || c.User2Id == userId));

        if (!canAccess)
        {
            throw new HubException("You do not have access to this conversation.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GetConversationGroup(conversationId));
    }

    public Task LeaveConversation(int conversationId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, GetConversationGroup(conversationId));
    }

    public static string GetConversationGroup(int conversationId) => $"conversation:{conversationId}";

    private int GetUserId()
    {
        var raw = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? Context.User?.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);

        return int.TryParse(raw, out var userId) ? userId : 0;
    }
}
