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

        public bool IsDeleted { get; set; } = false;

        public string? DeletedById { get; set; }
        public ApplicationUser? DeletedBy { get; set; }

        public DateTime? DeletedUtc { get; set; }

        public ICollection<MessageReport> Reports { get; set; } = new List<MessageReport>();
        public ICollection<ChatAttachment> Attachments { get; set; } = new List<ChatAttachment>();
    }
}
