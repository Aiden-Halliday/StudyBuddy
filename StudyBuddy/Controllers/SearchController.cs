using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.Models;
using StudyBuddy.Services;
using StudyBuddy.ViewModels.Search;
using System.Linq;

namespace StudyBuddy.Controllers
{
    public class SearchController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly CompatibilityService _compatibilityService;

        public SearchController(UserManager<ApplicationUser> userManager, ApplicationDbContext context, CompatibilityService compatibilityService)
        {
            _userManager = userManager;
            _context = context;
            _compatibilityService = compatibilityService;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return NotFound();
            }

            var userSkills = await _context.UserSkills
                .Include(us => us.Skill)
                .Where(us => us.UserId == user.Id)
                .OrderBy(us => us.Skill.Name)
                .ToListAsync();

            var otherUsers = await _context.Users
                .Where(u => u.Id != user.Id)
                .ToListAsync();

            var models = new List<SearchViewModel>();

            foreach (var otherUser in otherUsers)
            {
                var otherUserSkills = await _context.UserSkills
                    .Include(us => us.Skill)
                    .Where(us => us.UserId == otherUser.Id)
                    .OrderBy(us => us.Skill.Name)
                    .ToListAsync();

                var compatibilityResult = _compatibilityService.Calculate(userSkills, otherUserSkills);

                var model = new SearchViewModel
                {
                    DisplayName = otherUser.DisplayName,
                    Skills = otherUserSkills,
                    Score = compatibilityResult.Score
                };

                models.Add(model);
            }

            models = models
                .OrderByDescending(m => m.Score)
                .ToList();

            return View(models);
        }
    }
}
