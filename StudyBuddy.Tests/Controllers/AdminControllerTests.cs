using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Controllers;
using StudyBuddy.Data;
using StudyBuddy.Models;
using StudyBuddy.ViewModels.Admin;
using Xunit;

namespace StudyBuddy.Tests.Controllers;

public class AdminControllerTests
{
    // Each test class instance gets its own empty in-memory database,
    // so tests never see each other's data.
    private readonly DbContextOptions<ApplicationDbContext> _options =
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private ApplicationDbContext NewContext() => new(_options);

    private AdminController NewController(ApplicationDbContext context) => new(context);

    // ---------------------------------------------------------------
    // Index (user list)
    // ---------------------------------------------------------------

    [Fact]
    public async Task Index_NoUsers_ReturnsViewWithEmptyList()
    {
        using var context = NewContext();

        var result = await NewController(context).Index();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<List<AdminUserDisplayViewModel>>(view.Model);
        Assert.Empty(model);
    }

    [Fact]
    public async Task Index_ReturnsUsersOrderedByDisplayName()
    {
        await SeedUsersAsync(
            NewUser("Charlie", "charlie@example.com"),
            NewUser("Alice", "alice@example.com"),
            NewUser("Bob", "bob@example.com"));

        using var context = NewContext();
        var result = await NewController(context).Index();

        var model = GetModel<List<AdminUserDisplayViewModel>>(result);
        Assert.Equal(new[] { "Alice", "Bob", "Charlie" }, model.Select(u => u.DisplayName));
    }

    [Fact]
    public async Task Index_MapsUserFieldsToViewModel()
    {
        var created = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Utc);
        var user = NewUser("Alice", "alice@example.com");
        user.CreatedUtc = created;
        await SeedUsersAsync(user);

        using var context = NewContext();
        var result = await NewController(context).Index();

        var row = Assert.Single(GetModel<List<AdminUserDisplayViewModel>>(result));
        Assert.Equal(user.Id, row.Id);
        Assert.Equal("Alice", row.DisplayName);
        Assert.Equal("alice@example.com", row.Email);
        Assert.Equal(created, row.CreatedUtc);
    }

    [Fact]
    public async Task Index_UserWithNullEmail_ShowsEmptyString()
    {
        await SeedUsersAsync(NewUser("No Email", email: null));

        using var context = NewContext();
        var result = await NewController(context).Index();

        var row = Assert.Single(GetModel<List<AdminUserDisplayViewModel>>(result));
        Assert.Equal(string.Empty, row.Email);
    }

    // ---------------------------------------------------------------
    // Skills (skill list)
    // ---------------------------------------------------------------

    [Fact]
    public async Task Skills_NoSkills_ReturnsViewWithEmptyList()
    {
        using var context = NewContext();

        var result = await NewController(context).Skills();

        Assert.Empty(GetModel<List<AdminSkillViewModel>>(result));
    }

    [Fact]
    public async Task Skills_ReturnsSkillsOrderedByName()
    {
        await SeedSkillsAsync(
            new Skill { Name = "Physics" },
            new Skill { Name = "Calculus" },
            new Skill { Name = "Biology" });

        using var context = NewContext();
        var result = await NewController(context).Skills();

        var model = GetModel<List<AdminSkillViewModel>>(result);
        Assert.Equal(new[] { "Biology", "Calculus", "Physics" }, model.Select(s => s.Name));
    }

    [Fact]
    public async Task Skills_IncludesInactiveSkills_AndMapsFields()
    {
        await SeedSkillsAsync(
            new Skill { Name = "Calculus", Description = "Derivatives and integrals", IsActive = true },
            new Skill { Name = "Latin", Description = null, IsActive = false });

        using var context = NewContext();
        var result = await NewController(context).Skills();

        var model = GetModel<List<AdminSkillViewModel>>(result);
        Assert.Equal(2, model.Count);

        var calculus = model.Single(s => s.Name == "Calculus");
        Assert.True(calculus.SkillId > 0);
        Assert.Equal("Derivatives and integrals", calculus.Description);
        Assert.True(calculus.IsActive);

        var latin = model.Single(s => s.Name == "Latin");
        Assert.Null(latin.Description);
        Assert.False(latin.IsActive);
    }

    // ---------------------------------------------------------------
    // CreateSkill (GET)
    // ---------------------------------------------------------------

    [Fact]
    public void CreateSkill_Get_ReturnsViewWithEmptyViewModel()
    {
        using var context = NewContext();

        var result = NewController(context).CreateSkill();

        var model = GetModel<CreateSkillViewModel>(result);
        Assert.Equal(string.Empty, model.Name);
        Assert.Null(model.Description);
    }

    // ---------------------------------------------------------------
    // CreateSkill (POST)
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateSkill_Post_InvalidModelState_ReturnsView_AndSavesNothing()
    {
        var input = new Skill { Name = "" };

        using (var context = NewContext())
        {
            var controller = NewController(context);
            controller.ModelState.AddModelError("Name", "Required");

            var result = await controller.CreateSkill(input);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Same(input, view.Model);
        }

        using var check = NewContext();
        Assert.Empty(check.Skills);
    }

    [Fact]
    public async Task CreateSkill_Post_Valid_SavesSkillAndRedirectsToSkills()
    {
        using (var context = NewContext())
        {
            var result = await NewController(context).CreateSkill(
                new Skill { Name = "Calculus", Description = "Derivatives and integrals" });

            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal(nameof(AdminController.Skills), redirect.ActionName);
            Assert.Null(redirect.ControllerName);
        }

        using var check = NewContext();
        var saved = Assert.Single(check.Skills);
        Assert.Equal("Calculus", saved.Name);
        Assert.Equal("Derivatives and integrals", saved.Description);
    }

    [Fact]
    public async Task CreateSkill_Post_TrimsNameAndDescription()
    {
        using (var context = NewContext())
        {
            await NewController(context).CreateSkill(
                new Skill { Name = "  Calculus  ", Description = "  Limits  " });
        }

        using var check = NewContext();
        var saved = Assert.Single(check.Skills);
        Assert.Equal("Calculus", saved.Name);
        Assert.Equal("Limits", saved.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateSkill_Post_BlankDescription_IsSavedAsNull(string? description)
    {
        using (var context = NewContext())
        {
            await NewController(context).CreateSkill(
                new Skill { Name = "Calculus", Description = description });
        }

        using var check = NewContext();
        Assert.Null(Assert.Single(check.Skills).Description);
    }

    [Fact]
    public async Task CreateSkill_Post_AlwaysSavesAsActive_EvenIfInputSaysInactive()
    {
        // Guards against someone posting IsActive=false to create a hidden skill.
        using (var context = NewContext())
        {
            await NewController(context).CreateSkill(
                new Skill { Name = "Calculus", IsActive = false });
        }

        using var check = NewContext();
        Assert.True(Assert.Single(check.Skills).IsActive);
    }

    [Fact]
    public async Task CreateSkill_Post_DuplicateName_ReturnsViewWithError_AndDoesNotSave()
    {
        await SeedSkillsAsync(new Skill { Name = "Calculus" });
        var input = new Skill { Name = "Calculus" };

        using (var context = NewContext())
        {
            var controller = NewController(context);

            var result = await controller.CreateSkill(input);

            var view = Assert.IsType<ViewResult>(result);
            Assert.Same(input, view.Model);
            Assert.False(controller.ModelState.IsValid);

            var error = Assert.Single(controller.ModelState[nameof(Skill.Name)]!.Errors);
            Assert.Equal("A skill with this name already exists.", error.ErrorMessage);
        }

        using var check = NewContext();
        Assert.Single(check.Skills);
    }

    [Fact]
    public async Task CreateSkill_Post_DuplicateNameWithExtraSpaces_IsStillRejected()
    {
        await SeedSkillsAsync(new Skill { Name = "Calculus" });

        using (var context = NewContext())
        {
            var result = await NewController(context).CreateSkill(new Skill { Name = "  Calculus " });

            Assert.IsType<ViewResult>(result);
        }

        using var check = NewContext();
        Assert.Single(check.Skills);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static T GetModel<T>(IActionResult result)
    {
        var view = Assert.IsType<ViewResult>(result);
        return Assert.IsAssignableFrom<T>(view.Model);
    }

    private static ApplicationUser NewUser(string displayName, string? email) => new()
    {
        DisplayName = displayName,
        Email = email,
        UserName = email ?? displayName
    };

    private async Task SeedUsersAsync(params ApplicationUser[] users)
    {
        using var context = NewContext();
        context.Users.AddRange(users);
        await context.SaveChangesAsync();
    }

    private async Task SeedSkillsAsync(params Skill[] skills)
    {
        using var context = NewContext();
        context.Skills.AddRange(skills);
        await context.SaveChangesAsync();
    }
}
