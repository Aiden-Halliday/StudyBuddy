using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.Models;
using StudyBuddy.Services;
using StudyBuddy.ViewModels.Admin;

namespace StudyBuddy.Controllers;

[Authorize(Roles = AdminAccountStatus.AdminRole)]
[AutoValidateAntiforgeryToken]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AdminUserService _accounts;

    public AdminController(
        ApplicationDbContext context,
        AdminUserService accounts)
    {
        _context = context;
        _accounts = accounts;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search = null)
    {
        var query = _context.Users.AsNoTracking();

        search = search?.Trim();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(u =>
                u.DisplayName.Contains(search) ||
                (u.Email != null && u.Email.Contains(search)));
        }

        var users = await query
            .OrderBy(u => u.DisplayName)
            .ThenBy(u => u.Id)
            .ToListAsync();

        var adminIds = await (
            from membership in _context.UserRoles
            join role in _context.Roles
                on membership.RoleId equals role.Id
            where role.NormalizedName == "ADMIN"
            select membership.UserId
        ).ToListAsync();

        var admins = adminIds.ToHashSet();

        ViewData["Search"] = search;
        ViewData["CurrentUserId"] =
            HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);

        return View(users.Select(user => new AdminUserDisplayViewModel
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email ?? string.Empty,
            CreatedUtc = user.CreatedUtc,
            IsAdmin = admins.Contains(user.Id),
            IsDisabled = AdminAccountStatus.IsDisabled(user),
            IsLockedOut = AdminAccountStatus.IsLockedOut(user)
        }).ToList());
    }

    [HttpPost]
    public Task<IActionResult> Disable(string id) =>
        Change(id, AdminUserAction.Disable);

    [HttpPost]
    public Task<IActionResult> Enable(string id) =>
        Change(id, AdminUserAction.Enable);

    [HttpPost]
    public Task<IActionResult> GrantAdmin(string id) =>
        Change(id, AdminUserAction.GrantAdmin);

    [HttpPost]
    public Task<IActionResult> RemoveAdmin(string id) =>
        Change(id, AdminUserAction.RemoveAdmin);

    [HttpGet]
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest();

        var user = await _context.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == id);

        if (user == null)
            return NotFound();

        if (user.Id == User.FindFirstValue(ClaimTypes.NameIdentifier))
        {
            TempData["Error"] =
                "You cannot delete your own account from user management.";

            return RedirectToAction(nameof(Index));
        }

        return View(new DeleteUserViewModel
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email ?? string.Empty
        });
    }

    [HttpPost]
    [ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(
        DeleteUserViewModel model)
    {
        // Names and emails are read from the database, not trusted form fields.
        if (!ModelState.IsValid)
        {
            TempData["Error"] =
                "Enter the account email (or ID if it has no email) to confirm deletion.";

            return RedirectToAction(nameof(Delete), new { id = model.Id });
        }

        return await Change(
            model.Id,
            AdminUserAction.Delete,
            model.Confirmation);
    }

    private async Task<IActionResult> Change(
        string id,
        AdminUserAction action,
        string? confirmation = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            return BadRequest();

        var result = await _accounts.ChangeAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            id,
            action,
            confirmation);

        TempData[result.Succeeded ? "Success" : "Error"] = result.Message;

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Skills()
    {
        return View(await _context.Skills
            .AsNoTracking()
            .OrderBy(skill => skill.Name)
            .Select(skill => new AdminSkillViewModel
            {
                SkillId = skill.SkillId,
                Name = skill.Name,
                Description = skill.Description,
                IsActive = skill.IsActive
            })
            .ToListAsync());
    }

    [HttpGet]
    public IActionResult CreateSkill() =>
        View(new CreateSkillViewModel());

    [HttpPost]
    public async Task<IActionResult> CreateSkill(
        CreateSkillViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var name = model.Name?.Trim() ?? string.Empty;

        if (name.Length == 0)
        {
            ModelState.AddModelError(
                nameof(model.Name),
                "Enter a skill name.");

            return View(model);
        }

        if (await _context.Skills.AnyAsync(
            s => s.Name.ToUpper() == name.ToUpper()))
        {
            ModelState.AddModelError(
                nameof(model.Name),
                "A skill with this name already exists.");

            return View(model);
        }

        _context.Skills.Add(new Skill
        {
            Name = name,
            Description = string.IsNullOrWhiteSpace(model.Description)
                ? null
                : model.Description.Trim(),
            IsActive = true
        });

        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Skills));
    }
}