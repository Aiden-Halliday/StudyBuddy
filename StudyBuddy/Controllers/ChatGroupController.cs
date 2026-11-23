using Amazon.S3;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.Hubs;
using StudyBuddy.Models;
using StudyBuddy.Services;
using StudyBuddy.ViewModels.ChatGroup;
using System.Text.RegularExpressions;

namespace StudyBuddy.Controllers
{
    [Authorize]
    public class ChatGroupController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IChatService _chatService;
        private readonly CloudflareR2Service _r2Service;
        private readonly IHubContext<ChatHub> _chatHub;

        public ChatGroupController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IChatService chatService,
            CloudflareR2Service r2Service,
            IHubContext<ChatHub> chatHub)
        {
            _context = context;
            _userManager = userManager;
            _chatService = chatService;
            _r2Service = r2Service;
            _chatHub = chatHub;
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
                return NotFound();
            }

            var messages = await _context.ChatMessages
                .Include(m => m.Sender)
                .Include(m => m.Attachments)
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
        public async Task<IActionResult> ReportMessage(ReportMessageViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please provide a valid report.";
                return RedirectToAction(nameof(Details), new { id = model.ChatGroupId });
            }

            var message = await _context.ChatMessages
                .FirstOrDefaultAsync(m =>
                    m.ChatMessageId == model.ChatMessageId &&
                    m.ChatGroupId == model.ChatGroupId);

            if (message == null)
            {
                return NotFound();
            }

            var isMember = await _context.ChatGroupMembers
                .AnyAsync(m =>
                    m.ChatGroupId == model.ChatGroupId &&
                    m.UserId == user.Id);

            if (!isMember)
            {
                return Forbid();
            }

            var alreadyReported = await _context.MessageReports
                .AnyAsync(r =>
                    r.ChatMessageId == model.ChatMessageId &&
                    r.ReporterId == user.Id);

            if (alreadyReported)
            {
                TempData["Error"] = "You have already reported this message.";
                return RedirectToAction(nameof(Details), new { id = model.ChatGroupId });
            }

            var description = model.Description?.Trim();

            if (string.IsNullOrWhiteSpace(description))
            {
                description = null;
            }

            var report = new MessageReport
            {
                ChatMessageId = message.ChatMessageId,
                ReporterId = user.Id,
                Reason = model.Reason,
                Description = description,
                Status = MessageReportStatus.Pending,
                CreatedUtc = DateTime.UtcNow
            };

            _context.MessageReports.Add(report);

            await _context.SaveChangesAsync();

            TempData["Success"] = "Your report has been submitted successfully.";

            return RedirectToAction(nameof(Details), new { id = model.ChatGroupId });
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

            // Prevent suspended users from sending messages.
            if (user.SuspendedUntilUtc.HasValue &&
                user.SuspendedUntilUtc.Value > DateTime.UtcNow)
            {
                TempData["Error"] =
                    $"Your account is suspended until " +
                    $"{user.SuspendedUntilUtc.Value.ToLocalTime():g}.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = groupId });
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(100 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 100 * 1024 * 1024)]
        public async Task<IActionResult> UploadAttachments(int groupId, string? content, List<IFormFile> attachments)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var groupExists = await _context.ChatGroups
                .AnyAsync(g => g.ChatGroupId == groupId);

            if (!groupExists)
            {
                return NotFound();
            }

            var isMember = await _chatService.IsMemberAsync(
                groupId, user.Id);

            if (!isMember)
            {
                return Forbid();
            }

            try
            {
                var message = await _chatService.SendMessageAsync(groupId, user.Id, content ?? string.Empty, attachments);

                // Broadcast the new message and its attachments.
                await _chatHub.Clients.Group(groupId.ToString())
                    .SendAsync("ReceiveMessage", new
                    {
                        chatMessageId = message.ChatMessageId,
                        senderId = message.SenderId,
                        senderName = user.DisplayName,
                        profileImageUrl =
                            $"/Profile/Image?userId={Uri.EscapeDataString(user.Id)}",
                        content = message.Content,
                        sentUtc = message.SentUtc,

                        attachments = message.Attachments.Select(a => new
                        {
                            chatAttachmentId = a.ChatAttachmentId,
                            fileName = a.FileName,
                            contentType = a.ContentType,
                            fileSize = a.FileSize,
                            url = Url.Action(
                                nameof(DownloadAttachment),
                                "ChatGroup",
                                new { id = a.ChatAttachmentId })
                        }).ToList()
                    });

                return Ok(new
                {
                    success = true,
                    message = "Message sent successfully."
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
            catch (InvalidOperationException)
            {
                return StatusCode(500, new
                {
                    error = "The attachment upload could not be completed."
                });
            }
        }


        [HttpGet]
        public async Task<IActionResult> DownloadAttachment(int id)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return Unauthorized();
            }

            var attachment = await _context.ChatAttachments
                .Include(a => a.ChatMessage)
                .FirstOrDefaultAsync(a => a.ChatAttachmentId == id);

            if (attachment == null)
            {
                return NotFound();
            }

            var isMember = await _context.ChatGroupMembers
                .AnyAsync(m =>
                    m.ChatGroupId == attachment.ChatMessage.ChatGroupId &&
                    m.UserId == user.Id);

            if (!isMember)
            {
                return NotFound();
            }

            try
            {
                using var response = await _r2Service.GetObjectAsync(
                    attachment.StorageKey);

                var memoryStream = new MemoryStream();

                await response.ResponseStream.CopyToAsync(memoryStream);

                memoryStream.Position = 0;

                return File(memoryStream, attachment.ContentType);
            }
            catch (AmazonS3Exception)
            {
                return NotFound();
            }
        }
    }
}