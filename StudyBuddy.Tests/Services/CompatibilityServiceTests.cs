using System;
using System.Collections.Generic;
using System.Text;
using StudyBuddy.Models;
using StudyBuddy.Services;
using Xunit;

namespace StudyBuddy.Tests.Services;

public class CompatibilityServiceTests
{
    private readonly CompatibilityService _service = new();

    [Fact]
    public void Calculate_NoSharedSkills_ReturnsZeroScoreAndNoMutualMatch()
    {
        // Arrange
        var userASkills = new[]
        {
            new UserSkill { SkillId = 1, Type = SkillType.Strength }
        };

        var userBSkills = new[]
        {
            new UserSkill { SkillId = 2, Type = SkillType.Weakness }
        };

        // Act
        var result = _service.Calculate(userASkills, userBSkills);

        // Assert
        Assert.Equal(0, result.Score);
        Assert.False(result.IsMutualMatch);
    }

    [Fact]
    public void Calculate_EmptySkillLists_ReturnsZeroScoreAndNoMutualMatch()
    {
        // Arrange
        var userASkills = Array.Empty<UserSkill>();
        var userBSkills = Array.Empty<UserSkill>();

        // Act
        var result = _service.Calculate(userASkills, userBSkills);

        // Assert
        Assert.Equal(0, result.Score);
        Assert.False(result.IsMutualMatch);
    }

    [Fact]
    public void Calculate_UserAStrengthAndUserBWeakness_Returns100AndNoMutualMatch()
    {
        // Arrange
        var userASkills = new[]
        {
            new UserSkill { SkillId = 1, Type = SkillType.Strength }
        };

        var userBSkills = new[]
        {
            new UserSkill { SkillId = 1, Type = SkillType.Weakness }
        };

        // Act
        var result = _service.Calculate(userASkills, userBSkills);

        // Assert
        Assert.Equal(100.0, result.Score);
        Assert.False(result.IsMutualMatch);
    }

    [Fact]
    public void Calculate_UserBStrengthAndUserAWeakness_Returns100AndNoMutualMatch()
    {
        // Arrange
        var userASkills = new[]
        {
            new UserSkill { SkillId = 1, Type = SkillType.Weakness }
        };

        var userBSkills = new[]
        {
            new UserSkill { SkillId = 1, Type = SkillType.Strength }
        };

        // Act
        var result = _service.Calculate(userASkills, userBSkills);

        // Assert
        Assert.Equal(100.0, result.Score);
        Assert.False(result.IsMutualMatch);
    }

    [Fact]
    public void Calculate_MutualMatch_Returns100AndMutualMatch()
    {
        // Arrange
        var userASkills = new[]
        {
            new UserSkill { SkillId = 1, Type = SkillType.Strength },
            new UserSkill { SkillId = 2, Type = SkillType.Weakness }
        };

        var userBSkills = new[]
        {
            new UserSkill { SkillId = 1, Type = SkillType.Weakness },
            new UserSkill { SkillId = 2, Type = SkillType.Strength }
        };

        // Act
        var result = _service.Calculate(userASkills, userBSkills);

        // Assert
        Assert.Equal(100.0, result.Score);
        Assert.True(result.IsMutualMatch);
    }

    [Fact]
    public void Calculate_SharedStrengthsOnly_Returns100AndNoMutualMatch()
    {
        // Arrange
        var userASkills = new[]
        {
            new UserSkill { SkillId = 1, Type = SkillType.Strength },
            new UserSkill { SkillId = 2, Type = SkillType.Strength }
        };

        var userBSkills = new[]
        {
            new UserSkill { SkillId = 1, Type = SkillType.Strength },
            new UserSkill { SkillId = 2, Type = SkillType.Strength }
        };

        // Act
        var result = _service.Calculate(userASkills, userBSkills);

        // Assert
        Assert.Equal(25.0, result.Score);
        Assert.False(result.IsMutualMatch);
    }

    [Fact]
    public void Calculate_SharedWeaknessesOnly_ReturnsZeroAndNoMutualMatch()
    {
        // Arrange
        var userASkills = new[]
        {
            new UserSkill { SkillId = 1, Type = SkillType.Weakness }
        };

        var userBSkills = new[]
        {
            new UserSkill { SkillId = 1, Type = SkillType.Weakness }
        };

        // Act
        var result = _service.Calculate(userASkills, userBSkills);

        // Assert
        Assert.Equal(0.0, result.Score);
        Assert.False(result.IsMutualMatch);
    }

    [Fact]
    public void Calculate_MixedSharedSkills_ReturnsExpectedScore()
    {
        // Arrange
        var userASkills = new[]
        {
            new UserSkill { SkillId = 1, Type = SkillType.Strength },
            new UserSkill { SkillId = 2, Type = SkillType.Strength },
            new UserSkill { SkillId = 3, Type = SkillType.Weakness },
            new UserSkill { SkillId = 4, Type = SkillType.Weakness }
        };

        var userBSkills = new[]
        {
            new UserSkill { SkillId = 1, Type = SkillType.Weakness },
            new UserSkill { SkillId = 2, Type = SkillType.Strength },
            new UserSkill { SkillId = 3, Type = SkillType.Strength },
            new UserSkill { SkillId = 4, Type = SkillType.Weakness }
        };

        // Act
        var result = _service.Calculate(userASkills, userBSkills);

        // Assert
        Assert.Equal(61.1, result.Score);
        Assert.True(result.IsMutualMatch);
    }

    [Fact]
    public void Calculate_ScoreExceeds100_CapsScoreAt100()
    {
        // Arrange
        var userASkills = new[]
        {
            new UserSkill { SkillId = 1, Type = SkillType.Strength },
            new UserSkill { SkillId = 2, Type = SkillType.Strength },
            new UserSkill { SkillId = 3, Type = SkillType.Strength }
        };

        var userBSkills = new[]
        {
            new UserSkill { SkillId = 1, Type = SkillType.Weakness },
            new UserSkill { SkillId = 2, Type = SkillType.Weakness },
            new UserSkill { SkillId = 3, Type = SkillType.Weakness }
        };

        // Act
        var result = _service.Calculate(userASkills, userBSkills);

        // Assert
        Assert.Equal(100.0, result.Score);
        Assert.False(result.IsMutualMatch);
    }

    [Fact]
    public void Calculate_DuplicateSkillIds_ThrowsArgumentException()
    {
        // Arrange
        var userASkills = new[]
        {
            new UserSkill { SkillId = 1, Type = SkillType.Strength },
            new UserSkill { SkillId = 1, Type = SkillType.Weakness }
        };

        var userBSkills = new[]
        {
            new UserSkill { SkillId = 1, Type = SkillType.Strength }
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _service.Calculate(userASkills, userBSkills));
    }
}