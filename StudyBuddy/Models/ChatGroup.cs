namespace StudyBuddy.Models
{
    public class ChatGroup
    {
        public int ChatGroupId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string CreatedByUserId { get; set; } = string.Empty;
        public ApplicationUser CreatedBy { get; set; } = null!;

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public ICollection<ChatGroupMember> Members { get; set; }
            = new List<ChatGroupMember>();

        public ICollection<ChatMessage> Messages { get; set; }
            = new List<ChatMessage>();
    }
}
