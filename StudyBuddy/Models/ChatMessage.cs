namespace StudyBuddy.Models
{
    public class ChatMessage
    {
        public int ChatMessageId { get; set; }

        public int ChatGroupId { get; set; }
        public ChatGroup ChatGroup { get; set; } = null!;

        public string SenderId { get; set; } = string.Empty;
        public ApplicationUser Sender { get; set; } = null!;

        public string Content { get; set; } = string.Empty;

        public DateTime SentUtc { get; set; } = DateTime.UtcNow;
    }
}
