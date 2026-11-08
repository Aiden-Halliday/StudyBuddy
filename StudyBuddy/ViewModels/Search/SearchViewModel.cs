using StudyBuddy.Models;
using StudyBuddy.Services;

namespace StudyBuddy.ViewModels.Search
{
    public class SearchViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string DisplayName {  get; set; } = string.Empty;
        public string? ProfileImageKey { get; set; }
        public List<UserSkill> Skills { get; set; } = new();

        public string? ProfileImageUrl { get; set; }

        public double Score { get; set; }
        public bool HasSentRequest { get; set; }
        public bool IsFriend { get; set; }
    }
}