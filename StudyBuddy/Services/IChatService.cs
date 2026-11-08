using StudyBuddy.Models;

namespace StudyBuddy.Services
{
    public interface IChatService
    {
        Task<bool> IsMemberAsync(int groupId, string userId);

        Task<ChatMessage> SendMessageAsync(
            int groupId,
            string userId,
            string content);
    }
}
