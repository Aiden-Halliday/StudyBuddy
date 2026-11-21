using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.Models;
using StudyBuddy.Services;
using StudyBuddy.ViewModels.Admin;
using Microsoft.AspNetCore.SignalR;
using StudyBuddy.Hubs;

namespace StudyBuddy.Controllers;

[Authorize(Roles = AdminAccountStatus.AdminRole)]
[AutoValidateAntiforgeryToken]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AdminUserService _accounts;
    private readonly IModerationService _moderation;
    private readonly IHubContext<ChatHub> _chatHub;

    public AdminController(
        ApplicationDbContext context,
        AdminUserService accounts,
        IModerationService moderation,
        IHubContext<ChatHub> chatHub)
    {
        _context = context;
        _accounts = accounts;
        _moderation = moderation;
        _chatHub = chatHub;
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
    public async Task<IActionResult> Reports()
    {
        var reports = await _context.MessageReports
            .AsNoTracking()
            .Include(r => r.ChatMessage)
                .ThenInclude(m => m.ChatGroup)
            .Include(r => r.ChatMessage)
                .ThenInclude(m => m.Sender)
            .Include(r => r.Reporter)
            .OrderBy(r => r.Status == MessageReportStatus.Pending ? 0 : 1)
            .ThenByDescending(r => r.CreatedUtc)
            .Select(r => new AdminReportListItemViewModel
            {
                MessageReportId = r.MessageReportId,
                ChatMessageId = r.ChatMessageId,
                GroupName = r.ChatMessage.ChatGroup.Name,
                MessageSenderName = r.ChatMessage.Sender.DisplayName,
                ReporterName = r.Reporter.DisplayName,
                Reason = r.Reason,
                Status = r.Status,
                CreatedUtc = r.CreatedUtc
            })
            .ToListAsync();

        return View(reports);
    }

    [HttpGet]
    public async Task<IActionResult> ReportDetails(int id)
    {
        var report = await _context.MessageReports
            .AsNoTracking()
            .Include(r => r.ChatMessage)
                .ThenInclude(m => m.ChatGroup)
            .Include(r => r.ChatMessage)
                .ThenInclude(m => m.Sender)
            .Include(r => r.Reporter)
            .Include(r => r.ReviewedBy)
            .Include(r => r.Actions)
                .ThenInclude(a => a.Admin)
            .SingleOrDefaultAsync(r => r.MessageReportId == id);

        if (report == null)
            return NotFound();

        var model = new AdminReportDetailsViewModel
        {
            MessageReportId = report.MessageReportId,
            ChatMessageId = report.ChatMessageId,
            GroupName = report.ChatMessage.ChatGroup.Name,
            MessageContent = report.ChatMessage.Content,
            IsMessageDeleted = report.ChatMessage.IsDeleted,
            SenderSuspendedUntilUtc = report.ChatMessage.Sender.SuspendedUntilUtc,
            MessageSenderName = report.ChatMessage.Sender.DisplayName,
            MessageSentUtc = report.ChatMessage.SentUtc,
            ReporterName = report.Reporter.DisplayName,
            Reason = report.Reason,
            Description = report.Description,
            Status = report.Status,
            CreatedUtc = report.CreatedUtc,
            ReviewedByName = report.ReviewedBy?.DisplayName,
            ReviewedUtc = report.ReviewedUtc,

            ModerationHistory = report.Actions
            .OrderByDescending(a => a.CreatedUtc)
            .Select(a => new AdminModerationActionViewModel
            {
                ActionType = a.ActionType,
                AdminName = a.Admin.DisplayName,
                AdminComment = a.AdminComment,
                CreatedUtc = a.CreatedUtc,
                ExpiresUtc = a.ExpiresUtc
            })
            .ToList()
                };

                return View(model);
            }


    [HttpPost]
    public async Task<IActionResult> UpdateReportStatus(
        int id,
        MessageReportStatus status,
        string? adminComment)
    {
        if (id <= 0)
            return BadRequest();

        if (!Enum.IsDefined(typeof(MessageReportStatus), status))
        {
            TempData["Error"] = "The selected report status is invalid.";
            return RedirectToAction(nameof(ReportDetails), new { id });
        }

        var report = await _context.MessageReports
            .SingleOrDefaultAsync(r => r.MessageReportId == id);

        if (report == null)
            return NotFound();

        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(adminId))
            return Forbid();

        adminComment = adminComment?.Trim();

        if (string.IsNullOrWhiteSpace(adminComment))
        {
            TempData["Error"] = "Please provide a comment explaining this status change.";
            return RedirectToAction(nameof(ReportDetails), new { id });
        }

        if (adminComment.Length > 1000)
        {
            TempData["Error"] = "The comment cannot exceed 1000 characters.";
            return RedirectToAction(nameof(ReportDetails), new { id });
        }

        var previousStatus = report.Status;

        if (previousStatus == status)
        {
            TempData["Error"] = "The report already has that status.";
            return RedirectToAction(nameof(ReportDetails), new { id });
        }

        var now = DateTime.UtcNow;

        report.Status = status;

        // Record who last reviewed the report.
        if (status == MessageReportStatus.Pending)
        {
            report.ReviewedById = null;
            report.ReviewedUtc = null;
        }
        else
        {
            report.ReviewedById = adminId;
            report.ReviewedUtc = now;
        }

        // Save an audit record for this status change.
        _context.ModerationActions.Add(new ModerationAction
        {
            MessageReportId = report.MessageReportId,
            AdminId = adminId,
            ActionType = ModerationActionType.ReportStatusChanged,
            AdminComment =
                $"Status changed from {previousStatus} to {status}. {adminComment}",
            CreatedUtc = now
        });

        await _context.SaveChangesAsync();

        TempData["Success"] = "The report status was successfully updated.";

        return RedirectToAction(nameof(ReportDetails), new { id });
    }


    [HttpPost]
    public Task<IActionResult> IssueWarning(
        int id,
        string? adminComment)
    {
        return RunModerationAction(
            id,
            adminComment,
            (reportId, adminId, comment) =>
                _moderation.IssueWarningAsync(
                    reportId, adminId, comment));
    }

    [HttpPost]
    public Task<IActionResult> DeleteReportedMessage(
    int id,
    string? adminComment)
    {
        return RunModerationAction(
            id,
            adminComment,
            (reportId, adminId, comment) =>
                _moderation.DeleteMessageAsync(
                    reportId, adminId, comment),
            notifyChat: true);
    }

    [HttpPost]
    public Task<IActionResult> RestoreReportedMessage(
        int id,
        string? adminComment)
    {
        return RunModerationAction(
            id,
            adminComment,
            (reportId, adminId, comment) =>
                _moderation.RestoreMessageAsync(
                    reportId, adminId, comment),
            notifyChat: true);
    }

    [HttpPost]
    public Task<IActionResult> SuspendReportedUser(
        int id,
        string? adminComment,
        DateTime expiresUtc)
    {
        var expiration = DateTime.SpecifyKind(
            expiresUtc,
            DateTimeKind.Local).ToUniversalTime();

        return RunModerationAction(
            id,
            adminComment,
            (reportId, adminId, comment) =>
                _moderation.SuspendUserAsync(
                    reportId, adminId, comment, expiration));
    }

    [HttpPost]
    public Task<IActionResult> LiftUserSuspension(
        int id,
        string? adminComment)
    {
        return RunModerationAction(
            id,
            adminComment,
            (reportId, adminId, comment) =>
                _moderation.LiftSuspensionAsync(
                    reportId, adminId, comment));
    }

    [HttpPost]
    public Task<IActionResult> DismissReport(
        int id,
        string? adminComment)
    {
        return RunModerationAction(
            id,
            adminComment,
            (reportId, adminId, comment) =>
                _moderation.DismissReportAsync(
                    reportId, adminId, comment));
    }

    private async Task<IActionResult> RunModerationAction(
        int id,
        string? adminComment,
        Func<int, string, string, Task<ModerationResult>> action,
        bool notifyChat = false)
    {
        if (id <= 0)
            return BadRequest();

        var adminId = User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(adminId))
            return Forbid();

        var result = await action(
            id,
            adminId,
            adminComment ?? string.Empty);

        TempData[result.Succeeded ? "Success" : "Error"] =
            result.Message;

        if (result.Succeeded && notifyChat)
        {
            var report = await _context.MessageReports
                .Include(r => r.ChatMessage)
                .SingleAsync(r => r.MessageReportId == id);

            await _chatHub.Clients
                .Group(report.ChatMessage.ChatGroupId.ToString())
                .SendAsync("MessageModerationUpdated");
        }

        return RedirectToAction(
            nameof(ReportDetails),
            new { id });
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