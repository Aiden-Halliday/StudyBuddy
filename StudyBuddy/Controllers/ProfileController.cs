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
            CreatedUtc = user.CreatedUtc
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Skills()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user == null)
        {
            return NotFound();
        }

        var model = new ManageSkillsViewModel
        {
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
}
