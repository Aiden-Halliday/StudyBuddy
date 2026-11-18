using StudyBuddy.Models;

namespace StudyBuddy.ViewModels.ChatGroup
{
    public class ChatGroupDetailsViewModel
    {
        public StudyBuddy.Models.ChatGroup Group { get; set; } = null!;

        public List<ChatMessage> Messages { get; set; } = new();

        public string NewMessage { get; set; } = string.Empty;
    }
}
