using System.ComponentModel.DataAnnotations;

namespace StudyBuddy.Models;

public class Skill
{
    public int SkillId { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<UserSkill> UserSkills { get; set; } = new List<UserSkill>();
}
