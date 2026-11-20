
using System.ComponentModel.DataAnnotations;

namespace StudyBuddy.Models;

public class UserReputationVote
{
    public int Id { get; set; }

    [Required]
    public string VoterId { get; set; } = string.Empty;
    public ApplicationUser Voter { get; set; } = null!;

    [Required]
    public string TargetUserId { get; set; } = string.Empty;
    public ApplicationUser TargetUser { get; set; } = null!;

    public bool IsPositive { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
}
