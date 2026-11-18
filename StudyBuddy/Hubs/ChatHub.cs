using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using StudyBuddy.Services;

namespace StudyBuddy.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IChatService _chatService;

        public ChatHub(IChatService chatService)
        {
            _chatService = chatService;
        }

        public async Task JoinGroup(int groupId)
        {
            var userId = Context.UserIdentifier;

            if (string.IsNullOrEmpty(userId))
            {
                throw new HubException("You must be logged in.");
            }

            var isMember = await _chatService.IsMemberAsync(
                groupId, userId);

            if (!isMember)
            {
                throw new HubException(
                    "You are not a member of this chat group.");
            }

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                groupId.ToString());
        }

        public async Task LeaveGroup(int groupId)
        {
            await Groups.RemoveFromGroupAsync(
                Context.ConnectionId,
                groupId.ToString());
        }

        public async Task SendMessage(int groupId, string content)
        {
            var userId = Context.UserIdentifier;

            if (string.IsNullOrEmpty(userId))
            {
                throw new HubException("You must be logged in.");
            }

            try
            {
                var message = await _chatService.SendMessageAsync(
                    groupId,
                    userId,
                    content);

                await Clients.Group(groupId.ToString())
                    .SendAsync("ReceiveMessage", new
                    {
                        chatMessageId = message.ChatMessageId,
                        senderId = message.SenderId,
                        senderName = message.Sender.DisplayName,
                        profileImageUrl = $"/Profile/Image?userId={Uri.EscapeDataString(message.SenderId)}",
                        content = message.Content,
                        sentUtc = message.SentUtc
                    });
            }
            catch (ArgumentException ex)
            {
                throw new HubException(ex.Message);
            }
            catch (UnauthorizedAccessException)
            {
                throw new HubException(
                    "You are not a member of this chat group.");
            }
        }
    }
}