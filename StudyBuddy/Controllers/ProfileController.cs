using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.Models;
using StudyBuddy.ViewModels.Profile;

namespace StudyBuddy.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public ProfileController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    // GET: /Profile
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user == null)
        {
            return NotFound();
        }

        var model = new ProfileViewModel
        {
            DisplayName = user.DisplayName,
            Bio = user.Bio,
            ProfileImageKey = user.ProfileImageKey,
            CreatedUtc = user.CreatedUtc,

            Skills = await _context.UserSkills
                .Include(us => us.Skill)
                .Where(us => us.UserId == user.Id)
                .OrderBy(us => us.Skill.Name)
                .ToListAsync(),

            AvailableSkills = await _context.Skills
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync()
        };

        return View(model);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSkill(ProfileViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user == null)
        {
            return NotFound();
        }

        if (!Enum.IsDefined(typeof(SkillType), model.Type))
        {
            return BadRequest();
        }

        var skill = await _context.Skills.FirstOrDefaultAsync(s =>
                s.SkillId == model.SelectedSkillId &&
                s.IsActive);

        if (skill == null)
        {
            TempData["ErrorMessage"] = "Please select an available skill.";
            return RedirectToAction(nameof(Index));
        }

        var alreadyAdded = await _context.UserSkills
            .AnyAsync(us =>
                us.UserId == user.Id &&
                us.SkillId == skill.SkillId);

        if (alreadyAdded)
        {
            TempData["ErrorMessage"] = "You have already added this skill.";
            return RedirectToAction(nameof(Index));
        }

        var userSkill = new UserSkill
        {
            UserId = user.Id,
            SkillId = skill.SkillId,
            Type = model.Type
        };

        _context.UserSkills.Add(userSkill);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Skill added successfully.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSkill(int userSkillId)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user == null)
        {
            return NotFound();
        }

        var userSkill = await _context.UserSkills
            .FirstOrDefaultAsync(us =>
                us.UserSkillId == userSkillId &&
                us.UserId == user.Id);

        if (userSkill == null)
        {
            return NotFound();
        }

        _context.UserSkills.Remove(userSkill);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Skill removed successfully.";

        return RedirectToAction(nameof(Index));
    }

}
