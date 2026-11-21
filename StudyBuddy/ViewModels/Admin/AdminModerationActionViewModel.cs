using StudyBuddy.Models;

namespace StudyBuddy.ViewModels.Admin
{
    public class AdminModerationActionViewModel
    {
        public ModerationActionType ActionType { get; set; }

        public string AdminName { get; set; } = string.Empty;

        public string AdminComment { get; set; } = string.Empty;

        public DateTime CreatedUtc { get; set; }

        public DateTime? ExpiresUtc { get; set; }
    }
}