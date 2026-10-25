using System.ComponentModel.DataAnnotations;

namespace StudyBuddy.ViewModels.Admin
{
    public class CreateSkillViewModel
    {
        [Required]
        [StringLength(100)]
        [Display(Name = "Skill Name")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }
    }
}
