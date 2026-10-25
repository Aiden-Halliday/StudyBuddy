namespace StudyBuddy.ViewModels.Admin
{
    public class AdminUserDisplayViewModel
    {
        public required string Id { get; set; }

        public string DisplayName { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public DateTime CreatedUtc { get; set; }
    }
}
