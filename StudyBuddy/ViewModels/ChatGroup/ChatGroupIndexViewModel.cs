using StudyBuddy.Models;

namespace StudyBuddy.ViewModels.ChatGroup
{
    public class ChatGroupIndexViewModel
    {
        public List<ChatGroupIndexItemViewModel> Groups { get; set; }
            = new();
    }

    public class ChatGroupIndexItemViewModel
    {
        public StudyBuddy.Models.ChatGroup Group { get; set; } = null!;

        public List<ApplicationUser> AvailableFriends { get; set; }
            = new();

        public bool IsCreator { get; set; }
    }
}
