using System.ComponentModel.DataAnnotations;

namespace StudyBuddy.Models
{
    public class UserSkill
    {
        public int UserSkillId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public ApplicationUser User { get; set; } = null!;

        [Required]
        [StringLength(100)]
        public string SkillName { get; set; } = string.Empty;

        public SkillType Type { get; set; }
    }

    public enum SkillType
    {
        Strength = 0,
        Weakness = 1
    }
}
