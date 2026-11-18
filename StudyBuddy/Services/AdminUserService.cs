using System.Data;
using System.Data.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.Models;

namespace StudyBuddy.Services;

public enum AdminUserAction
{
    Disable,
    Enable,
    GrantAdmin,
    RemoveAdmin,
    Delete
}

public record AdminUserResult(bool Succeeded, string Message);

public class AdminUserService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ILogger<AdminUserService> _logger;

    public AdminUserService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> users,
        ILogger<AdminUserService> logger)
    {
        _context = context;
        _users = users;
        _logger = logger;
    }

    public async Task<AdminUserResult> ChangeAsync(
        string? actorId,
        string id,
        AdminUserAction action,
        string? confirmation = null)
    {
        // UserManager shares this scoped DbContext and transaction.
        await using var transaction =
            await _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        try
        {
            // Cookie validation may already have tracked this account.
            // Read fresh database state inside the transaction.
            var actor = actorId == null
                ? null
                : await _context.Users
                    .AsNoTracking()
                    .SingleOrDefaultAsync(u => u.Id == actorId);

            if (actor == null ||
                AdminAccountStatus.IsLockedOut(actor) ||
                !await _users.IsInRoleAsync(
                    actor,
                    AdminAccountStatus.AdminRole))
            {
                return new(
                    false,
                    "Your administrator access is no longer available.");
            }

            var user = await _users.FindByIdAsync(id);

            if (user == null)
                return new(false, "This account no longer exists.");

            if (actorId == id)
            {
                return new(
                    false,
                    "You cannot change your own account from user management.");
            }

            var isAdmin = await _users.IsInRoleAsync(
                user,
                AdminAccountStatus.AdminRole);

            if (isAdmin &&
                action is AdminUserAction.Disable
                    or AdminUserAction.RemoveAdmin
                    or AdminUserAction.Delete)
            {
                var admins = await _users.GetUsersInRoleAsync(
                    AdminAccountStatus.AdminRole);

                if (!admins.Any(a =>
                    a.Id != id &&
                    !AdminAccountStatus.IsLockedOut(a)))
                {
                    return new(
                        false,
                        "At least one active administrator must remain.");
                }
            }

            if (action == AdminUserAction.GrantAdmin &&
                AdminAccountStatus.IsLockedOut(user))
            {
                return new(
                    false,
                    "Enable or unlock this account before assigning the admin role.");
            }

            if (action == AdminUserAction.Delete &&
                !string.Equals(
                    confirmation?.Trim(),
                    user.Email ?? user.Id,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new(
                    false,
                    "The confirmation must match the account email (or ID if it has no email).");
            }

            switch (action)
            {
                case AdminUserAction.Disable:
                    EnsureSuccess(
                        await _users.SetLockoutEnabledAsync(user, true));

                    EnsureSuccess(
                        await _users.SetLockoutEndDateAsync(
                            user,
                            DateTimeOffset.MaxValue));

                    EnsureSuccess(
                        await _users.UpdateSecurityStampAsync(user));

                    break;

                case AdminUserAction.Enable:
                    EnsureSuccess(
                        await _users.SetLockoutEndDateAsync(user, null));

                    EnsureSuccess(
                        await _users.ResetAccessFailedCountAsync(user));

                    break;

                case AdminUserAction.GrantAdmin:
                    if (!isAdmin)
                    {
                        EnsureSuccess(
                            await _users.AddToRoleAsync(
                                user,
                                AdminAccountStatus.AdminRole));
                    }

                    break;

                case AdminUserAction.RemoveAdmin:
                    if (isAdmin)
                    {
                        EnsureSuccess(
                            await _users.RemoveFromRoleAsync(
                                user,
                                AdminAccountStatus.AdminRole));
                    }

                    break;

                case AdminUserAction.Delete:
                    // Clean up relationships that would otherwise prevent deletion.
                    var ownedGroups = _context.ChatGroups
                        .Where(g => g.CreatedByUserId == id)
                        .Select(g => g.ChatGroupId);

                    await _context.ChatMessages
                        .Where(m =>
                            m.SenderId == id ||
                            ownedGroups.Contains(m.ChatGroupId))
                        .ExecuteDeleteAsync();

                    await _context.ChatGroupMembers
                        .Where(m =>
                            m.UserId == id ||
                            ownedGroups.Contains(m.ChatGroupId))
                        .ExecuteDeleteAsync();

                    await _context.ChatGroups
                        .Where(g => g.CreatedByUserId == id)
                        .ExecuteDeleteAsync();

                    await _context.FriendRequests
                        .Where(r =>
                            r.SenderId == id ||
                            r.ReceiverId == id)
                        .ExecuteDeleteAsync();

                    await _context.UserSkills
                        .Where(s => s.UserId == id)
                        .ExecuteDeleteAsync();

                    EnsureSuccess(await _users.DeleteAsync(user));

                    break;

                default:
                    return new(false, "Unknown account action.");
            }

            await transaction.CommitAsync();

            _logger.LogInformation(
                "Admin {ActorId} performed {Action} on account {UserId}",
                actorId,
                action,
                id);

            return new(true, action switch
            {
                AdminUserAction.Disable =>
                    "Account disabled. Existing sessions are rejected on their next request.",

                AdminUserAction.Enable =>
                    "Account enabled and login lockout cleared.",

                AdminUserAction.GrantAdmin =>
                    "Admin role assigned. It takes effect on the user's next request.",

                AdminUserAction.RemoveAdmin =>
                    "Admin role removed. It takes effect on the user's next request.",

                _ => "Account and related database records deleted."
            });
        }
        catch (Exception ex) when (
            ex is DbUpdateException
                or DbException
                or IdentityOperationException)
        {
            await transaction.RollbackAsync();

            _context.ChangeTracker.Clear();

            _logger.LogWarning(
                ex,
                "Admin account change failed for {UserId}",
                id);

            return new(
                false,
                "The account could not be updated. Refresh and try again; " +
                "see the application logs if it persists.");
        }
    }

    private static void EnsureSuccess(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new IdentityOperationException(
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    private sealed class IdentityOperationException(string message)
        : Exception(message);
}