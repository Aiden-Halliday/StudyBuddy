
using StudyBuddy.Models;

namespace StudyBuddy.ViewModels.Search
{
    public class SearchViewModel
    {
        public string UserId { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string? ProfileImageKey { get; set; }

        public string? ProfileImageUrl { get; set; }

        public List<UserSkill> Skills { get; set; } = new();

        public double Score { get; set; }

        public bool HasSentRequest { get; set; }

        public bool IsFriend { get; set; }

        // Reputation properties
        public int TotalVotes { get; set; }

        public int PositiveVotes { get; set; }

        public double? HelpfulPercentage => TotalVotes == 0
            ? null
            : Math.Round(
                100.0 * PositiveVotes / TotalVotes,
                0,
                MidpointRounding.AwayFromZero);
    }
}
