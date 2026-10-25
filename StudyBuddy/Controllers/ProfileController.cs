using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using StudyBuddy.Models;
using StudyBuddy.ViewModels.Profile;

namespace StudyBuddy.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ProfileController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
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

    // GET: /Profile/Edit
    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user == null)
        {
            return NotFound();
        }

        var model = new EditProfileViewModel
        {
            DisplayName = user.DisplayName,
            Bio = user.Bio
        };

        return View(model);
    }

    // POST: /Profile/Edit
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditProfileViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);

        if (user == null)
        {
            return NotFound();
        }

        user.DisplayName = model.DisplayName;
        user.Bio = model.Bio;
        user.UpdatedUtc = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        TempData["SuccessMessage"] = "Your profile has been updated.";

        return RedirectToAction(nameof(Index));
    }
}
