using StudyBuddy.Models;
using StudyBuddy.Services;

namespace StudyBuddy.ViewModels.Search
{
    public class SearchViewModel
    {
        public string DisplayName {  get; set; } = string.Empty;

        public List<UserSkill> Skills { get; set; } = new();

        public double Score { get; set; }
    }
}
