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

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user == null)
        {
            return NotFound();
        }

        var friendships = await _context.FriendRequests
            .Include(fr => fr.Sender)
            .Include(fr => fr.Receiver)
            .Where(fr =>
                fr.Status == FriendRequestStatus.Accepted &&
                (fr.SenderId == user.Id || fr.ReceiverId == user.Id))
            .ToListAsync();

        var friends = friendships
            .Select(fr => fr.SenderId == user.Id ? fr.Receiver : fr.Sender)
            .OrderBy(friend => friend.DisplayName)
            .ToList();

        return View(friends);
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveFriend(string id)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user == null)
        {
            return NotFound();
        }

        var friendship = await _context.FriendRequests
            .FirstOrDefaultAsync(fr =>
                fr.Status == FriendRequestStatus.Accepted &&
                (
                    (fr.SenderId == user.Id && fr.ReceiverId == id) ||
                    (fr.ReceiverId == user.Id && fr.SenderId == id)
                ));

        if (friendship == null)
        {
            return NotFound();
        }

        _context.FriendRequests.Remove(friendship);

        await _context.SaveChangesAsync();

        TempData["Success"] = "Friend removed successfully.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendRequest(string id)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user == null)
        {
            return NotFound();
        }

        // Prevent sending a request to yourself.
        if (user.Id == id)
        {
            TempData["Error"] = "You cannot send a friend request to yourself.";
            return RedirectToAction("Index", "Search");
        }

        var receiver = await _userManager.FindByIdAsync(id);

        if (receiver == null)
        {
            return NotFound();
        }

        var existingRequest = await _context.FriendRequests
            .AnyAsync(fr =>
                (fr.SenderId == user.Id && fr.ReceiverId == id ||
                 fr.SenderId == id && fr.ReceiverId == user.Id) &&
                (fr.Status == FriendRequestStatus.Pending ||
                 fr.Status == FriendRequestStatus.Accepted));

        if (existingRequest)
        {
            TempData["Error"] = "A pending request or friendship already exists.";
            return RedirectToAction("Index", "Search");
        }

        var request = new FriendRequest
        {
            SenderId = user.Id,
            ReceiverId = id,
            Status = FriendRequestStatus.Pending,
            CreatedUtc = DateTime.UtcNow
        };

        _context.FriendRequests.Add(request);

        await _context.SaveChangesAsync();

        TempData["Success"] = "Friend request sent successfully.";

        return RedirectToAction("Index", "Search");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelRequest(string id)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user == null)
        {
            return NotFound();
        }

        var request = await _context.FriendRequests
            .FirstOrDefaultAsync(fr =>
                fr.SenderId == user.Id &&
                fr.ReceiverId == id &&
                fr.Status == FriendRequestStatus.Pending);

        if (request == null)
        {
            return NotFound();
        }

        _context.FriendRequests.Remove(request);

        await _context.SaveChangesAsync();

        TempData["Success"] = "Friend request cancelled.";

        return RedirectToAction("Index", "Search");
    }
}
