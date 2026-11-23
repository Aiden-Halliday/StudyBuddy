
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.Models;

namespace StudyBuddy.Controllers;

[Authorize]
public class ReputationController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReputationController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Vote(
        string userId,
        bool? isPositive)
    {
        var voterId = _userManager.GetUserId(User);

        if (string.IsNullOrWhiteSpace(voterId))
            return Challenge();

        if (string.IsNullOrWhiteSpace(userId) || isPositive is null)
            return BadRequest();

        // Users cannot rate themselves.
        if (userId == voterId)
            return BadRequest("You cannot rate your own profile.");

        // Verify that the student being rated exists.
        if (!await _context.Users.AnyAsync(u => u.Id == userId))
            return NotFound();

        // Find an existing vote from this student.
        var vote = await _context.UserReputationVotes
            .SingleOrDefaultAsync(v =>
                v.VoterId == voterId &&
                v.TargetUserId == userId);

        if (vote is null)
        {
            // First vote: create a new record.
            _context.UserReputationVotes.Add(
                new UserReputationVote
                {
                    VoterId = voterId,
                    TargetUserId = userId,
                    IsPositive = isPositive.Value
                });
        }
        else if (vote.IsPositive != isPositive.Value)
        {
            // Existing vote: update instead of duplicating.
            vote.IsPositive = isPositive.Value;
            vote.UpdatedUtc = DateTime.UtcNow;
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // The unique index prevents duplicate votes
            // even if requests are submitted simultaneously.
            return Conflict(
                "Could not save this vote. Refresh and try again.");
        }

        TempData["SuccessMessage"] = "Your rating has been saved.";

        return RedirectToAction(
            "DedicatedProfile",
            "Profile",
            new { id = userId });
    }
}
