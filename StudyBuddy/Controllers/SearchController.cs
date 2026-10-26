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
            var otherUsers = await _context.Users
            .Where(u => u.Id != user.Id)
            .ToListAsync();

            if (user == null)
            {
                return NotFound();
            }

            var models = new List<SearchViewModel>();

            foreach (var otherUser in otherUsers)
            {
                var compatibilityResult = _compatibilityService.Calculate(user.Skills, otherUser.Skills);
                var model = new SearchViewModel
                {
                    DisplayName = user.DisplayName,
                    Skills = await _context.UserSkills
                    .Include(us => us.Skill)
                    .Where(us => us.UserId == user.Id)
                    .OrderBy(us => us.Skill.Name)
                    .ToListAsync(),
                    Score = compatibilityResult.Score
                };

                models.Add(model);
            }
            models = models.OrderByDescending(m => m.Score).ToList();
            return View(models);
        }
    }
}
