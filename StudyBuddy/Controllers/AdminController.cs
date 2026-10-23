using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.ViewModels.Admin;

namespace StudyBuddy.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }
        public async Task<IActionResult> Index()
        {
            var users = await _context.Users
                .AsNoTracking()
                .OrderBy(user => user.DisplayName)
                .Select(user => new AdminUserDisplayViewModel
                {
                    Id = user.Id,
                    DisplayName = user.DisplayName,
                    Email = user.Email ?? string.Empty,
                    CreatedUtc = user.CreatedUtc
                })
                .ToListAsync();

            return View(users);
        }
    }
}
