using StudyBuddy.Models;
using System.ComponentModel.DataAnnotations;

namespace StudyBuddy.ViewModels.Profile
{
    public class ManageSkillsViewModel
    {
        [Required]
        [StringLength(100)]
        [Display(Name = "Skill name")]
        public string SkillName { get; set; } = string.Empty;

        public SkillType Type { get; set; } = SkillType.Strength;

        public List<UserSkill> Skills { get; set; } = new();
    }
}
