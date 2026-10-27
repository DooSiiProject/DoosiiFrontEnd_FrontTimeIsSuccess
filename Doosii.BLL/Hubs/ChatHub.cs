using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Doosii.BLL.DTOs;
using Doosii.BLL.Exceptions;
using Doosii.BLL.Interfaces;
using Doosii.DAL.Data;

namespace Doosii.BLL.Hubs;

[Authorize]
public class ChatHub(IChatService chatService, AppDbContext db) : Hub
{
    private int UserId() => int.TryParse(Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) && id > 0
        ? id : throw new HubException("UNAUTHORIZED");

    public override async Task OnConnectedAsync()
    {
        var id = UserId();
        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == id && u.Role != "Banned"))
        {
            Context.Abort();
            throw new HubException("CHAT_ACCESS_DENIED");
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{id}");
        await base.OnConnectedAsync();
    }

    private async Task Execute(Func<Task> action)
    {
        try { await action(); }
        catch (ServiceException ex)
        {
            await Clients.Caller.SendAsync("ChatError", new { Code = ex.ErrorCode, Message = ex.Message });
            throw new HubException(ex.ErrorCode);
        }
        catch (HubException ex)
        {
            await Clients.Caller.SendAsync("ChatError", new { Code = ex.Message, Message = "Conversation access denied" });
            throw;
        }
        catch
        {
            await Clients.Caller.SendAsync("ChatError", new { Code = "CHAT_OPERATION_FAILED", Message = "Chat operation failed" });
            throw new HubException("CHAT_OPERATION_FAILED");
        }
    }

    private async Task<int> Member(int conversationId)
    {
        var id = UserId();
        if (conversationId <= 0 || !await chatService.IsMemberAsync(conversationId, id))
            throw new HubException("CHAT_ACCESS_DENIED");
        return id;
    }

    // Failed joins reject invoke(), so the client cannot treat a denied room as joined.
    public Task JoinConversation(int conversationId) => Execute(async () =>
    {
        var id = await Member(conversationId);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
        await Clients.Group($"conversation_{conversationId}").SendAsync("UserJoined", new { UserId = id, ConversationId = conversationId });
    });

    public Task LeaveConversation(int conversationId) => Execute(async () =>
    {
        var id = await Member(conversationId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"conversation_{conversationId}");
        await Clients.Group($"conversation_{conversationId}").SendAsync("UserLeft", new { UserId = id, ConversationId = conversationId });
    });

    // FE writes remain REST + clientRequestId. Legacy hub writes keep service-side
    // membership, banned-account, closed-conversation and product ownership checks.
    public Task SendMessage(int conversationId, string content) => Execute(async () =>
    {
        var message = await chatService.SendTextMessageAsync(conversationId, UserId(), content);
        await Clients.Group($"conversation_{conversationId}").SendAsync("ReceiveMessage", message);
        await InboxChanged(conversationId);
    });

    public Task SendProductCard(int conversationId, int productId) => Execute(async () =>
    {
        var message = await chatService.SendProductCardAsync(conversationId, UserId(), productId);
        await Clients.Group($"conversation_{conversationId}").SendAsync("ReceiveProductCard", message);
        await InboxChanged(conversationId);
    });

    public Task MarkMessagesAsRead(int conversationId) => Execute(async () =>
    {
        var status = await chatService.MarkMessagesAsReadAsync(conversationId, UserId());
        await Clients.Group($"conversation_{conversationId}").SendAsync("MessagesRead", status);
        await InboxChanged(conversationId);
    });

    private async Task InboxChanged(int conversationId)
    {
        try {
            var conversation = await chatService.GetConversationAsync(conversationId, UserId());
            await Clients.Groups(new[] { $"user_{conversation.BuyerId}", $"user_{conversation.SellerId}" })
                .SendAsync("ConversationChanged", new { ConversationId = conversationId });
        } catch { /* Stored data is recovered by REST. */ }
    }

    public Task SendTyping(int conversationId, bool isTyping) => Execute(async () =>
    {
        var id = UserId();
        var conversation = await chatService.GetConversationAsync(conversationId, id);
        if (conversation.IsClosed) throw new HubException("CONVERSATION_CLOSED");
        await Clients.OthersInGroup($"conversation_{conversationId}").SendAsync("UserTyping",
            new UserTypingDto { ConversationId = conversationId, UserId = id, IsTyping = isTyping });
    });
}
