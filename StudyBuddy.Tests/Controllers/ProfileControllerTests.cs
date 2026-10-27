using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using StudyBuddy.Controllers;
using StudyBuddy.Data;
using StudyBuddy.Models;
using StudyBuddy.ViewModels.Profile;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace StudyBuddy.Tests.Controllers;

public class ProfileControllerTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly ApplicationDbContext _context;
    private readonly ProfileController _controller;

    private readonly ApplicationUser _user;

    public ProfileControllerTests()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();

        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            store.Object,
            Mock.Of<IOptions<IdentityOptions>>(),
            Mock.Of<IPasswordHasher<ApplicationUser>>(),
            Array.Empty<IUserValidator<ApplicationUser>>(),
            Array.Empty<IPasswordValidator<ApplicationUser>>(),
            Mock.Of<ILookupNormalizer>(),
            Mock.Of<IdentityErrorDescriber>(),
            Mock.Of<IServiceProvider>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<UserManager<ApplicationUser>>>());

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);

        _user = new ApplicationUser
        {
            Id = "user-1",
            DisplayName = "Test Student",
            Bio = "Computer science student",
            ProfileImageKey = "profile-1",
            CreatedUtc = DateTime.UtcNow
        };

        _userManagerMock
            .Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync(_user);

        _controller = new ProfileController(
            _userManagerMock.Object,
            _context);

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[] { new Claim(ClaimTypes.NameIdentifier, _user.Id) },
                    "TestAuth"))
        };

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        _controller.TempData = new TempDataDictionary(
            httpContext,
            Mock.Of<ITempDataProvider>());
    }

    private async Task SeedSkillAsync(
        int skillId,
        string name,
        bool isActive = true)
    {
        _context.Skills.Add(new Skill
        {
            SkillId = skillId,
            Name = name,
            IsActive = isActive
        });

        await _context.SaveChangesAsync();
    }

    private async Task<UserSkill> SeedUserSkillAsync(
        int skillId,
        SkillType type = SkillType.Strength,
        string? userId = null,
        string skillName = "Programming")
    {
        var skill = await _context.Skills
            .FirstOrDefaultAsync(s => s.SkillId == skillId);

        if (skill == null)
        {
            skill = new Skill
            {
                SkillId = skillId,
                Name = skillName,
                IsActive = true
            };

            _context.Skills.Add(skill);
            await _context.SaveChangesAsync();
        }

        var userSkill = new UserSkill
        {
            UserId = userId ?? _user.Id,
            SkillId = skillId,
            Type = type
        };

        _context.UserSkills.Add(userSkill);
        await _context.SaveChangesAsync();

        return userSkill;
    }

    // --------------------
    // Index tests
    // --------------------

    [Fact]
    public async Task Index_UserExists_ReturnsViewWithProfile()
    {
        // Act
        var result = await _controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ProfileViewModel>(viewResult.Model);

        Assert.Equal(_user.DisplayName, model.DisplayName);
        Assert.Equal(_user.Bio, model.Bio);
        Assert.Equal(_user.ProfileImageKey, model.ProfileImageKey);
        Assert.Equal(_user.CreatedUtc, model.CreatedUtc);
    }

    [Fact]
    public async Task Index_UserDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        _userManagerMock
            .Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _controller.Index();

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Index_ReturnsOnlyActiveAvailableSkills()
    {
        // Arrange
        await SeedSkillAsync(1, "Algorithms", true);
        await SeedSkillAsync(2, "Databases", false);
        await SeedSkillAsync(3, "Programming", true);

        // Act
        var result = await _controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ProfileViewModel>(viewResult.Model);

        Assert.Equal(2, model.AvailableSkills.Count);
        Assert.All(
            model.AvailableSkills,
            skill => Assert.True(skill.IsActive));

        Assert.Equal(
            new[] { "Algorithms", "Programming" },
            model.AvailableSkills.Select(s => s.Name).ToArray());
    }

    [Fact]
    public async Task Index_ReturnsUserSkillsOrderedBySkillName()
    {
        // Arrange
        await SeedSkillAsync(1, "Zoology");
        await SeedSkillAsync(2, "Algorithms");

        await SeedUserSkillAsync(1, skillName: "Zoology");
        await SeedUserSkillAsync(2, skillName: "Algorithms");

        // Act
        var result = await _controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ProfileViewModel>(viewResult.Model);

        Assert.Equal(
            new[] { "Algorithms", "Zoology" },
            model.Skills.Select(s => s.Skill.Name).ToArray());
    }

    [Fact]
    public async Task Index_DoesNotIncludeOtherUsersSkills()
    {
        // Arrange
        await SeedUserSkillAsync(1, userId: _user.Id);
        await SeedUserSkillAsync(2, userId: "another-user");

        // Act
        var result = await _controller.Index();

        // Assert
        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ProfileViewModel>(viewResult.Model);

        var skill = Assert.Single(model.Skills);
        Assert.Equal(1, skill.SkillId);
        Assert.Equal(_user.Id, skill.UserId);
    }

    // --------------------
    // AddSkill tests
    // --------------------

    [Fact]
    public async Task AddSkill_ValidSkill_AddsSkillAndRedirects()
    {
        // Arrange
        await SeedSkillAsync(1, "Programming");

        var model = new ProfileViewModel
        {
            SelectedSkillId = 1,
            Type = SkillType.Strength
        };

        // Act
        var result = await _controller.AddSkill(model);

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ProfileController.Index), redirect.ActionName);

        var addedSkill = await _context.UserSkills.SingleAsync();

        Assert.Equal(_user.Id, addedSkill.UserId);
        Assert.Equal(1, addedSkill.SkillId);
        Assert.Equal(SkillType.Strength, addedSkill.Type);

        Assert.Equal(
            "Skill added successfully.",
            _controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task AddSkill_UserDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        _userManagerMock
            .Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        var model = new ProfileViewModel
        {
            SelectedSkillId = 1,
            Type = SkillType.Strength
        };

        // Act
        var result = await _controller.AddSkill(model);

        // Assert
        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(await _context.UserSkills.ToListAsync());
    }

    [Fact]
    public async Task AddSkill_InvalidSkillType_ReturnsBadRequest()
    {
        // Arrange
        var model = new ProfileViewModel
        {
            SelectedSkillId = 1,
            Type = (SkillType)999
        };

        // Act
        var result = await _controller.AddSkill(model);

        // Assert
        Assert.IsType<BadRequestResult>(result);
        Assert.Empty(await _context.UserSkills.ToListAsync());
    }

    [Theory]
    [InlineData(1, false)] // Inactive skill
    [InlineData(999, true)] // Nonexistent skill
    public async Task AddSkill_UnavailableSkill_SetsErrorAndRedirects(
        int skillId,
        bool seedDifferentActiveSkill)
    {
        // Arrange
        if (skillId == 1)
        {
            await SeedSkillAsync(1, "Inactive skill", false);
        }

        if (seedDifferentActiveSkill)
        {
            await SeedSkillAsync(2, "Other active skill");
        }

        var model = new ProfileViewModel
        {
            SelectedSkillId = skillId,
            Type = SkillType.Strength
        };

        // Act
        var result = await _controller.AddSkill(model);

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ProfileController.Index), redirect.ActionName);

        Assert.Equal(
            "Please select an available skill.",
            _controller.TempData["ErrorMessage"]);

        Assert.Empty(await _context.UserSkills.ToListAsync());
    }

    [Fact]
    public async Task AddSkill_AlreadyAdded_SetsErrorAndDoesNotAddDuplicate()
    {
        // Arrange
        await SeedSkillAsync(1, "Programming");
        await SeedUserSkillAsync(1, SkillType.Strength);

        var model = new ProfileViewModel
        {
            SelectedSkillId = 1,
            Type = SkillType.Weakness
        };

        // Act
        var result = await _controller.AddSkill(model);

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ProfileController.Index), redirect.ActionName);

        Assert.Equal(
            "You have already added this skill.",
            _controller.TempData["ErrorMessage"]);

        var skills = await _context.UserSkills.ToListAsync();
        var skill = Assert.Single(skills);

        Assert.Equal(SkillType.Strength, skill.Type);
    }

    [Fact]
    public async Task AddSkill_SkillAddedByAnotherUser_AllowsCurrentUserToAddIt()
    {
        // Arrange
        await SeedSkillAsync(1, "Programming");
        await SeedUserSkillAsync(1, userId: "another-user");

        var model = new ProfileViewModel
        {
            SelectedSkillId = 1,
            Type = SkillType.Strength
        };

        // Act
        var result = await _controller.AddSkill(model);

        // Assert
        Assert.IsType<RedirectToActionResult>(result);

        var skills = await _context.UserSkills
            .Where(s => s.SkillId == 1)
            .ToListAsync();

        Assert.Equal(2, skills.Count);
        Assert.Contains(skills, s => s.UserId == _user.Id);
        Assert.Contains(skills, s => s.UserId == "another-user");
    }

    // --------------------
    // RemoveSkill tests
    // --------------------

    [Fact]
    public async Task RemoveSkill_ExistingUserSkill_RemovesSkillAndRedirects()
    {
        // Arrange
        var userSkill = await SeedUserSkillAsync(1);

        // Act
        var result = await _controller.RemoveSkill(userSkill.UserSkillId);

        // Assert
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(ProfileController.Index), redirect.ActionName);

        Assert.Empty(await _context.UserSkills.ToListAsync());

        Assert.Equal(
            "Skill removed successfully.",
            _controller.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task RemoveSkill_UserDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var userSkill = await SeedUserSkillAsync(1);

        _userManagerMock
            .Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
            .ReturnsAsync((ApplicationUser?)null);

        // Act
        var result = await _controller.RemoveSkill(userSkill.UserSkillId);

        // Assert
        Assert.IsType<NotFoundResult>(result);
        Assert.Single(await _context.UserSkills.ToListAsync());
    }

    [Fact]
    public async Task RemoveSkill_SkillDoesNotExist_ReturnsNotFound()
    {
        // Act
        var result = await _controller.RemoveSkill(999);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task RemoveSkill_SkillBelongsToAnotherUser_ReturnsNotFound()
    {
        // Arrange
        var userSkill = await SeedUserSkillAsync(
            1,
            userId: "another-user");

        // Act
        var result = await _controller.RemoveSkill(userSkill.UserSkillId);

        // Assert
        Assert.IsType<NotFoundResult>(result);

        var remainingSkill = await _context.UserSkills.SingleAsync();
        Assert.Equal("another-user", remainingSkill.UserId);
    }
}