using System.ComponentModel.DataAnnotations;

namespace StudyBuddy.Models
{
    public class MessageReport
    {
        public int MessageReportId { get; set; }

        public int ChatMessageId { get; set; }
        public ChatMessage ChatMessage { get; set; } = null!;

        public string ReporterId { get; set; } = string.Empty;
        public ApplicationUser Reporter { get; set; } = null!;

        public MessageReportReason Reason { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }

        public MessageReportStatus Status { get; set; } = MessageReportStatus.Pending;

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public string? ReviewedById { get; set; }

        public ApplicationUser? ReviewedBy { get; set; }

        public DateTime? ReviewedUtc { get; set; }

        public ICollection<ModerationAction> Actions { get; set; } = new List<ModerationAction>();
    }
}
