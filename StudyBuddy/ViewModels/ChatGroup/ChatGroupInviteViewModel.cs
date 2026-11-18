using StudyBuddy.Models;


namespace StudyBuddy.ViewModels.ChatGroup
{
    public class ChatGroupInviteViewModel
    {
        public int ChatGroupId { get; set; }

        public string GroupName { get; set; } = string.Empty;

        public List<ApplicationUser> Friends { get; set; } = new();
    }
}
