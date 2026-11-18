using Microsoft.AspNetCore.Identity;
using StudyBuddy.Models;

namespace StudyBuddy.Services;

public static class AdminAccountStatus
{
    public const string AdminRole = "Admin";

    // A maximum lockout date means an administrator disabled the account.
    // Finite lockout dates remain distinguishable from permanent disable.
    public static bool IsDisabled(ApplicationUser user) =>
        user.LockoutEnabled &&
        user.LockoutEnd == DateTimeOffset.MaxValue;

    public static bool IsLockedOut(ApplicationUser user) =>
        user.LockoutEnabled &&
        user.LockoutEnd > DateTimeOffset.UtcNow;
}