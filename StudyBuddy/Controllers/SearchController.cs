
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.Models;
using StudyBuddy.Services;
using StudyBuddy.ViewModels.Search;

namespace StudyBuddy.Controllers;

[Authorize]
public class SearchController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly CompatibilityService _compatibilityService;

    public SearchController(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        CompatibilityService compatibilityService)
    {
        _userManager = userManager;
        _context = context;
        _compatibilityService = compatibilityService;
    }

    public async Task<IActionResult> Index()
    {
        var results = await BuildResultsAsync(null);

        if (results is null)
            return NotFound();

        return View(results);
    }

    [HttpGet]
    public async Task<IActionResult> SearchBar(string? searchBarInput)
    {
        if (string.IsNullOrWhiteSpace(searchBarInput))
            return RedirectToAction(nameof(Index));

        var query = searchBarInput.Trim();

        var results = await BuildResultsAsync(query);

        if (results is null)
            return NotFound();

        ViewData["Query"] = query;

        return View(nameof(Index), results);
    }

    private async Task<List<SearchViewModel>?> BuildResultsAsync(
        string? query)
    {
        var currentUser = await _userManager.GetUserAsync(User);

        if (currentUser is null)
            return null;

        var otherUsersQuery = _context.Users
            .AsNoTracking()
            .Where(u => u.Id != currentUser.Id);

        if (!string.IsNullOrWhiteSpace(query))
        {
            otherUsersQuery = otherUsersQuery
                .Where(u => u.DisplayName.Contains(query));
        }

        var otherUsers = await otherUsersQuery.ToListAsync();

        var otherUserIds = otherUsers
            .Select(u => u.Id)
            .ToList();

        // Get the current student's skills.
        var currentSkills = await _context.UserSkills
            .AsNoTracking()
            .Include(s => s.Skill)
            .Where(s => s.UserId == currentUser.Id)
            .ToListAsync();

        // Get the skills of students being considered.
        var otherSkills = await _context.UserSkills
            .AsNoTracking()
            .Include(s => s.Skill)
            .Where(s => otherUserIds.Contains(s.UserId))
            .ToListAsync();

        var skillsByUser = otherSkills
            .GroupBy(s => s.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Load positive and total vote counts per student.
        var voteCounts = await _context.UserReputationVotes
            .AsNoTracking()
            .Where(v => otherUserIds.Contains(v.TargetUserId))
            .GroupBy(v => v.TargetUserId)
            .Select(g => new
            {
                UserId = g.Key,
                Total = g.Count(),
                Positive = g.Count(v => v.IsPositive)
            })
            .ToListAsync();

        var votesByUser = voteCounts
            .ToDictionary(v => v.UserId);

        // Pending friend requests.
        var pendingUserIds = await _context.FriendRequests
            .AsNoTracking()
            .Where(fr =>
                fr.SenderId == currentUser.Id &&
                fr.Status == FriendRequestStatus.Pending)
            .Select(fr => fr.ReceiverId)
            .ToListAsync();

        // Existing friends.
        var friendUserIds = await _context.FriendRequests
            .AsNoTracking()
            .Where(fr =>
                fr.Status == FriendRequestStatus.Accepted &&
                (fr.SenderId == currentUser.Id ||
                 fr.ReceiverId == currentUser.Id))
            .Select(fr => fr.SenderId == currentUser.Id
                ? fr.ReceiverId
                : fr.SenderId)
            .ToListAsync();

        var pendingSet = pendingUserIds.ToHashSet();
        var friendSet = friendUserIds.ToHashSet();

        var results = new List<SearchViewModel>();

        foreach (var otherUser in otherUsers)
        {
            var skills = skillsByUser.TryGetValue(
                otherUser.Id, out var found)
                ? found
                : new List<UserSkill>();

            var score = _compatibilityService
                .Calculate(currentSkills, skills)
                .Score;

            var hasVotes = votesByUser.TryGetValue(
                otherUser.Id, out var votes);

            results.Add(new SearchViewModel
            {
                UserId = otherUser.Id,
                DisplayName = otherUser.DisplayName,
                ProfileImageKey = otherUser.ProfileImageKey,
                Skills = skills,

                Score = score,

                HasSentRequest =
                    pendingSet.Contains(otherUser.Id),

                IsFriend =
                    friendSet.Contains(otherUser.Id),

                TotalVotes =
                    hasVotes ? votes!.Total : 0,

                PositiveVotes =
                    hasVotes ? votes!.Positive : 0
            });
        }

        // Reputation ranking:
        // 1. Students with votes before unrated students.
        // 2. Higher helpfulness percentages first.
        // 3. Compatibility breaks ties.
        // 4. Display name breaks remaining ties.
        return results
            .OrderByDescending(m => m.TotalVotes > 0)
            .ThenByDescending(m => m.HelpfulPercentage ?? 0)
            .ThenByDescending(m => m.Score)
            .ThenBy(m => m.DisplayName)
            .ToList();
    }
}
