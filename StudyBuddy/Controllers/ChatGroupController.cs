using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.Models;
using StudyBuddy.ViewModels.ChatGroup;

namespace StudyBuddy.Controllers
{
    [Authorize]
    public class ChatGroupController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ChatGroupController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private async Task<bool> AreFriendsAsync(string userId, string otherUserId)
        {
            return await _context.FriendRequests.AnyAsync(fr =>
                fr.Status == FriendRequestStatus.Accepted &&
                (
                    (fr.SenderId == userId && fr.ReceiverId == otherUserId) ||
                    (fr.SenderId == otherUserId && fr.ReceiverId == userId)
                ));
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return NotFound();
            }

            // Get groups the current user belongs to.
            var groups = await _context.ChatGroups
                .Include(g => g.Members)
                .Where(g => g.Members.Any(m => m.UserId == user.Id))
                .OrderBy(g => g.Name)
                .ToListAsync();

            // Get accepted friendships involving the current user.
            var friendships = await _context.FriendRequests
                .Include(fr => fr.Sender)
                .Include(fr => fr.Receiver)
                .Where(fr =>
                    fr.Status == FriendRequestStatus.Accepted &&
                    (fr.SenderId == user.Id || fr.ReceiverId == user.Id))
                .ToListAsync();

            // Build a list of the current user's friends.
            var friends = friendships
                .Select(fr => fr.SenderId == user.Id
                    ? fr.Receiver
                    : fr.Sender)
                .DistinctBy(friend => friend.Id)
                .OrderBy(friend => friend.DisplayName)
                .ToList();

            var model = new ChatGroupIndexViewModel();

            foreach (var group in groups)
            {
                var isCreator = group.CreatedByUserId == user.Id;

                // Find friends who are not already members of this group.
                var availableFriends = isCreator
                    ? friends
                        .Where(friend => !group.Members.Any(m => m.UserId == friend.Id))
                        .ToList()
                    : new List<ApplicationUser>();

                model.Groups.Add(new ChatGroupIndexItemViewModel
                {
                    Group = group,
                    IsCreator = isCreator,
                    AvailableFriends = availableFriends
                });
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string name)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return NotFound();
            }

            name = name?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(name))
            {
                ModelState.AddModelError("name", "Group name is required.");
                return View();
            }

            if (name.Length > 100)
            {
                ModelState.AddModelError("name",
                    "Group name cannot exceed 100 characters.");

                return View();
            }

            var group = new ChatGroup
            {
                Name = name,
                CreatedByUserId = user.Id,
                CreatedUtc = DateTime.UtcNow
            };

            group.Members.Add(new ChatGroupMember
            {
                UserId = user.Id,
                JoinedUtc = DateTime.UtcNow
            });

            _context.ChatGroups.Add(group);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Chat group created successfully.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddMember(int groupId, string userId)
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
            {
                return NotFound();
            }

            var group = await _context.ChatGroups
                .FirstOrDefaultAsync(g => g.ChatGroupId == groupId);

            if (group == null)
            {
                return NotFound();
            }

            if (group.CreatedByUserId != currentUser.Id)
            {
                return Forbid();
            }

            if (!await AreFriendsAsync(currentUser.Id, userId))
            {
                TempData["Error"] = "You can only invite friends to chat groups.";
                return RedirectToAction(nameof(Index));
            }

            var alreadyMember = await _context.ChatGroupMembers
                .AnyAsync(m => m.ChatGroupId == groupId && m.UserId == userId);

            if (alreadyMember)
            {
                TempData["Error"] = "This user is already in the group.";
                return RedirectToAction(nameof(Index));
            }

            var member = new ChatGroupMember
            {
                ChatGroupId = groupId,
                UserId = userId,
                JoinedUtc = DateTime.UtcNow
            };

            _context.ChatGroupMembers.Add(member);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Friend added to the group.";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Invite(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);

            if (currentUser == null)
            {
                return NotFound();
            }

            var group = await _context.ChatGroups
                .FirstOrDefaultAsync(g => g.ChatGroupId == id);

            if (group == null)
            {
                return NotFound();
            }

            // Only the group creator can invite members.
            if (group.CreatedByUserId != currentUser.Id)
            {
                return Forbid();
            }

            // Get users who are already members.
            var existingMemberIds = await _context.ChatGroupMembers
                .Where(m => m.ChatGroupId == id)
                .Select(m => m.UserId)
                .ToListAsync();

            // Get accepted friendships involving the current user.
            var friendships = await _context.FriendRequests
                .Include(fr => fr.Sender)
                .Include(fr => fr.Receiver)
                .Where(fr =>
                    fr.Status == FriendRequestStatus.Accepted &&
                    (fr.SenderId == currentUser.Id ||
                     fr.ReceiverId == currentUser.Id))
                .ToListAsync();

            // Get friends who are not already in the group.
            var friends = friendships
                .Select(fr => fr.SenderId == currentUser.Id
                    ? fr.Receiver
                    : fr.Sender)
                .Where(friend => !existingMemberIds.Contains(friend.Id))
                .OrderBy(friend => friend.DisplayName)
                .ToList();

            var model = new ChatGroupInviteViewModel
            {
                ChatGroupId = group.ChatGroupId,
                GroupName = group.Name,
                Friends = friends
            };

            return PartialView("_InviteFriends", model);
        }

        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return NotFound();
            }

            var group = await _context.ChatGroups
                .Include(g => g.CreatedBy)
                .Include(g => g.Members)
                    .ThenInclude(m => m.User)
                .FirstOrDefaultAsync(g => g.ChatGroupId == id);

            if (group == null)
            {
                return NotFound();
            }

            bool isMember = group.Members.Any(m => m.UserId == user.Id);

            if (!isMember)
            {
                return Forbid();
            }

            var messages = await _context.ChatMessages
                .Include(m => m.Sender)
                .Where(m => m.ChatGroupId == id)
                .OrderBy(m => m.SentUtc)
                .ToListAsync();

            var model = new ChatGroupDetailsViewModel
            {
                Group = group,
                Messages = messages
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendMessage(int groupId, string content)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return NotFound();
            }

            var isMember = await _context.ChatGroupMembers
                .AnyAsync(m =>
                    m.ChatGroupId == groupId &&
                    m.UserId == user.Id);

            if (!isMember)
            {
                return Forbid();
            }

            content = content?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(content))
            {
                TempData["Error"] = "Message cannot be empty.";
                return RedirectToAction(nameof(Details), new { id = groupId });
            }

            if (content.Length > 2000)
            {
                TempData["Error"] = "Message cannot exceed 2000 characters.";
                return RedirectToAction(nameof(Details), new { id = groupId });
            }

            var message = new ChatMessage
            {
                ChatGroupId = groupId,
                SenderId = user.Id,
                Content = content,
                SentUtc = DateTime.UtcNow
            };

            _context.ChatMessages.Add(message);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = groupId });
        }
    }
}