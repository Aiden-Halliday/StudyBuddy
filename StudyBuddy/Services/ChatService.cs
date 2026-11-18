using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.Models;

namespace StudyBuddy.Services
{
    public class ChatService : IChatService
    {
        private readonly ApplicationDbContext _context;

        public ChatService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<bool> IsMemberAsync(
            int groupId,
            string userId)
        {
            return await _context.ChatGroupMembers
                .AnyAsync(m => m.ChatGroupId == groupId &&
                               m.UserId == userId);
        }

        public async Task<ChatMessage> SendMessageAsync(
            int groupId,
            string userId,
            string content)
        {
            content = content?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException(
                    "Message cannot be empty.");
            }

            if (content.Length > 2000)
            {
                throw new ArgumentException(
                    "Message cannot exceed 2000 characters.");
            }

            var isMember = await IsMemberAsync(groupId, userId);

            if (!isMember)
            {
                throw new UnauthorizedAccessException(
                    "You are not a member of this chat group.");
            }

            var message = new ChatMessage
            {
                ChatGroupId = groupId,
                SenderId = userId,
                Content = content,
                SentUtc = DateTime.UtcNow
            };

            _context.ChatMessages.Add(message);

            await _context.SaveChangesAsync();

            await _context.Entry(message)
                .Reference(m => m.Sender)
                .LoadAsync();

            return message;
        }
    }
}
