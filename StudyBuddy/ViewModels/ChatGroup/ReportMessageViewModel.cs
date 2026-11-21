
using System.ComponentModel.DataAnnotations;
using StudyBuddy.Models;

namespace StudyBuddy.ViewModels.ChatGroup
{
    public class ReportMessageViewModel
    {
        [Range(1, int.MaxValue)]
        public int ChatMessageId { get; set; }

        [Range(1, int.MaxValue)]
        public int ChatGroupId { get; set; }

        [EnumDataType(typeof(MessageReportReason))]
        public MessageReportReason Reason { get; set; }

        [StringLength(1000)]
        public string? Description { get; set; }
    }
}