using StudyBuddy.Models;

namespace StudyBuddy.Services;

public class CompatibilityService
{
    public CompatibilityResult Calculate(IEnumerable<UserSkill> userASkills, IEnumerable<UserSkill> userBSkills)
    {

        //puts ALL skills that the users have into dictionaries
        var skillsA = userASkills.ToDictionary(s => s.SkillId, s => s.Type);
        var skillsB = userBSkills.ToDictionary(s => s.SkillId, s => s.Type);

        //checks if there are any matching skills
        var sharedSkillIds = skillsA.Keys.Intersect(skillsB.Keys).ToList();

        if (sharedSkillIds.Count == 0)
        {
            return new CompatibilityResult
            {
                Score = 0,
                IsMutualMatch = false
            };
        }

        //student A can help student B count
        int aHelpsB = sharedSkillIds.Count(id =>
            skillsA[id] == SkillType.Strength &&
            skillsB[id] == SkillType.Weakness);

        //student B can help student A count
        int bHelpsA = sharedSkillIds.Count(id =>
            skillsB[id] == SkillType.Strength &&
            skillsA[id] == SkillType.Weakness);

        //students are strong in the same area count
        int sharedStrengths = sharedSkillIds.Count(id =>
            skillsA[id] == SkillType.Strength &&
            skillsB[id] == SkillType.Strength);

        //determines whether both students can get value out of helping each other mutually 
        bool isMutual = aHelpsB > 0 && bHelpsA > 0;

        double score = (aHelpsB + bHelpsA) * 40 + sharedStrengths * 10;

        if (isMutual)
        {
            score += 20;
        }

        double maxScore = sharedSkillIds.Count * 40 + (isMutual ? 20 : 0);

        return new CompatibilityResult
        {
            Score = Math.Round(Math.Min(100, score / maxScore * 100), 1),
            IsMutualMatch = isMutual
        };
    }
}

public class CompatibilityResult
{
    public double Score { get; set; }
    public bool IsMutualMatch { get; set; }
}