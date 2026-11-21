
using StudyBuddy.Models;

namespace StudyBuddy.ViewModels.Admin
{
    public class AdminReportDetailsViewModel
    {
        public int MessageReportId { get; set; }

        public int ChatMessageId { get; set; }

        public string GroupName { get; set; } = string.Empty;

        public string MessageContent { get; set; } = string.Empty;

        public bool IsMessageDeleted { get; set; }

        public DateTime? SenderSuspendedUntilUtc { get; set; }

        public string MessageSenderName { get; set; } = string.Empty;

        public DateTime MessageSentUtc { get; set; }

        public string ReporterName { get; set; } = string.Empty;

        public MessageReportReason Reason { get; set; }

        public string? Description { get; set; }

        public MessageReportStatus Status { get; set; }

        public DateTime CreatedUtc { get; set; }

        public string? ReviewedByName { get; set; }

        public DateTime? ReviewedUtc { get; set; }

        public ICollection<AdminModerationActionViewModel> ModerationHistory
        { get; set; } = new List<AdminModerationActionViewModel>();
    }
}
