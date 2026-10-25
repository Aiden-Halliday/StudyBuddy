using StudyBuddy.Models;

namespace StudyBuddy.ViewModels.Profile;

public class ManageSkillsViewModel
{
    public int SelectedSkillId { get; set; }

    public SkillType Type { get; set; } = SkillType.Strength;

    public List<UserSkill> Skills { get; set; } = new();

    public List<Skill> AvailableSkills { get; set; } = new();
}
