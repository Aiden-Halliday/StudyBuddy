
using StudyBuddy.Models;

namespace StudyBuddy.ViewModels.Profile
{
    public class ProfileViewModel
    {
        public string UserId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string? Bio { get; set; }

        public string? ProfileImageKey { get; set; }

        public string? ProfileImageUrl { get; set; }

        public DateTime CreatedUtc { get; set; }

        public List<UserSkill> Skills { get; set; } = new();

        public List<Skill> AvailableSkills { get; set; } = new();

        public int SelectedSkillId { get; set; }

        public SkillType Type { get; set; } = SkillType.Strength;

        // Reputation properties
        public int TotalVotes { get; set; }

        public int PositiveVotes { get; set; }

        public bool? CurrentUserVote { get; set; }

        public double? HelpfulPercentage => TotalVotes == 0
            ? null
            : Math.Round(
                100.0 * PositiveVotes / TotalVotes,
                0,
                MidpointRounding.AwayFromZero);
    }
}
