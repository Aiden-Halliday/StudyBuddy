
using StudyBuddy.Models;

namespace StudyBuddy.ViewModels.Profile
{
    public class ProfileViewModel
    {
        public string DisplayName { get; set; } = string.Empty;

        public string? Bio { get; set; }

        public string? ProfileImageKey { get; set; }

        public DateTime CreatedUtc { get; set; }

        public List<UserSkill> Skills { get; set; } = new();

        public List<Skill> AvailableSkills { get; set; } = new();

        public int SelectedSkillId { get; set; }

        public SkillType Type { get; set; } = SkillType.Strength;
    }
}
