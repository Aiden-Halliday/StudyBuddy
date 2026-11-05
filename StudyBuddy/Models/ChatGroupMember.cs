namespace StudyBuddy.Models
{
    public class ChatGroupMember
    {
        public int ChatGroupMemberId { get; set; }

        public int ChatGroupId { get; set; }
        public ChatGroup ChatGroup { get; set; } = null!;

        public string UserId { get; set; } = string.Empty;
        public ApplicationUser User { get; set; } = null!;

        public DateTime JoinedUtc { get; set; } = DateTime.UtcNow;
    }
}
