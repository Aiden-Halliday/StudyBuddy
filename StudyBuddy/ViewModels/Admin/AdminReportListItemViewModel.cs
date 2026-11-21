
using StudyBuddy.Models;

namespace StudyBuddy.ViewModels.Admin
{
    public class AdminReportListItemViewModel
    {
        public int MessageReportId { get; set; }

        public int ChatMessageId { get; set; }

        public string GroupName { get; set; } = string.Empty;

        public string MessageSenderName { get; set; } = string.Empty;

        public string ReporterName { get; set; } = string.Empty;

        public MessageReportReason Reason { get; set; }

        public MessageReportStatus Status { get; set; }

        public DateTime CreatedUtc { get; set; }
    }
}
