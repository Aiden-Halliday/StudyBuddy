using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.Models;

namespace StudyBuddy.Controllers;

[Authorize]
public class FriendController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public FriendController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Requests()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user == null)
        {
            return NotFound();
        }

        var requests = await _context.FriendRequests
            .Include(fr => fr.Sender)
            .Where(fr =>
                fr.ReceiverId == user.Id &&
                fr.Status == FriendRequestStatus.Pending)
            .OrderByDescending(fr => fr.CreatedUtc)
            .ToListAsync();

        return View(requests);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptRequest(int id)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user == null)
        {
            return NotFound();
        }

        var request = await _context.FriendRequests
            .FirstOrDefaultAsync(fr =>
                fr.FriendRequestId == id &&
                fr.ReceiverId == user.Id &&
                fr.Status == FriendRequestStatus.Pending);

        if (request == null)
        {
            return NotFound();
        }

        request.Status = FriendRequestStatus.Accepted;

        await _context.SaveChangesAsync();

        TempData["Success"] = "Friend request accepted.";

        return RedirectToAction(nameof(Requests));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectRequest(int id)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user == null)
        {
            return NotFound();
        }

        var request = await _context.FriendRequests
            .FirstOrDefaultAsync(fr =>
                fr.FriendRequestId == id &&
                fr.ReceiverId == user.Id &&
                fr.Status == FriendRequestStatus.Pending);

        if (request == null)
        {
            return NotFound();
        }

        request.Status = FriendRequestStatus.Rejected;

        await _context.SaveChangesAsync();

        TempData["Success"] = "Friend request rejected.";

        return RedirectToAction(nameof(Requests));
    }
}
