
using System.ComponentModel.DataAnnotations;

namespace StudyBuddy.Models
{
    public class ModerationAction
    {
        public int ModerationActionId { get; set; }

        public int MessageReportId { get; set; }
        public MessageReport MessageReport { get; set; } = null!;

        public string AdminId { get; set; } = string.Empty;
        public ApplicationUser Admin { get; set; } = null!;

        public ModerationActionType ActionType { get; set; }

        [Required]
        [StringLength(1000)]
        public string AdminComment { get; set; } = string.Empty;

        public DateTime CreatedUtc { get; set; }
            = DateTime.UtcNow;

        public DateTime? ExpiresUtc { get; set; }
    }
}

