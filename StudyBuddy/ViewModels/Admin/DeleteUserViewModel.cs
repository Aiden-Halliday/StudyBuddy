using System.ComponentModel.DataAnnotations;

namespace StudyBuddy.ViewModels.Admin;

public class DeleteUserViewModel
{
    [Required]
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Confirm email or account ID")]
    public string Confirmation { get; set; } = string.Empty;
}