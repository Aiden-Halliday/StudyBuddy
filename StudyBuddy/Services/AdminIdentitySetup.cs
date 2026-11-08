using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using StudyBuddy.Models;

namespace StudyBuddy.Services;

public static class AdminIdentitySetup
{
    public static async Task ValidatePrincipalAsync(
        CookieValidatePrincipalContext context)
    {
        // Retain Identity security-stamp validation.
        // With interval zero, role claims are rebuilt on each request.
        await SecurityStampValidator.ValidatePrincipalAsync(context);

        if (context.Principal?.Identity?.IsAuthenticated != true)
            return;

        var users = context.HttpContext.RequestServices
            .GetRequiredService<UserManager<ApplicationUser>>();

        var user = await users.GetUserAsync(context.Principal);

        if (user == null || await users.IsLockedOutAsync(user))
        {
            context.RejectPrincipal();

            await context.HttpContext.SignOutAsync(
                IdentityConstants.ApplicationScheme);
        }
    }

    public static async Task InitializeAsync(
        IServiceProvider services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var roles =
            services.GetRequiredService<RoleManager<IdentityRole>>();

        var users =
            services.GetRequiredService<UserManager<ApplicationUser>>();

        if (!await roles.RoleExistsAsync(AdminAccountStatus.AdminRole))
        {
            RequireSuccess(await roles.CreateAsync(
                new IdentityRole(AdminAccountStatus.AdminRole)));
        }

        // Local, explicitly configured first-admin setup.
        // Register the account first, set the secret, then restart.
        var email = configuration["AdminBootstrap:Email"];

        if (!environment.IsDevelopment() ||
            string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        if ((await users.GetUsersInRoleAsync(
            AdminAccountStatus.AdminRole)).Count != 0)
        {
            return;
        }

        var user = await users.FindByEmailAsync(email.Trim());

        if (user == null || AdminAccountStatus.IsLockedOut(user))
        {
            throw new InvalidOperationException(
                "The configured development admin account must already exist " +
                "and be unlocked. Register it first, or remove AdminBootstrap:Email.");
        }

        RequireSuccess(await users.AddToRoleAsync(
            user,
            AdminAccountStatus.AdminRole));
    }

    private static void RequireSuccess(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}