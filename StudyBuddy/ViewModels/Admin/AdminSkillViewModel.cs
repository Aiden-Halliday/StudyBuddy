namespace StudyBuddy.ViewModels.Admin
{
    public class AdminSkillViewModel
    {
        public int SkillId { get; set; }

        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsActive { get; set; }
    }
}
