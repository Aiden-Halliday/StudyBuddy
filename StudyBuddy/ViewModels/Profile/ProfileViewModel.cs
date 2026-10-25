
namespace StudyBuddy.ViewModels.Profile
{
    public class ProfileViewModel
    {
        public string DisplayName { get; set; } = string.Empty;

        public string? Bio { get; set; }

        public string? ProfileImageKey { get; set; }

        public DateTime CreatedUtc { get; set; }
    }
}
