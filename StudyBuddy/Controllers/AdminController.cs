using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.Models;
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

        // GET: /Admin
        [HttpGet]
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

        // GET: /Admin/Skills
        [HttpGet]
        public async Task<IActionResult> Skills()
        {
            var skills = await _context.Skills
                .AsNoTracking()
                .OrderBy(skill => skill.Name)
                .Select(skill => new AdminSkillViewModel
                {
                    SkillId = skill.SkillId,
                    Name = skill.Name,
                    Description = skill.Description,
                    IsActive = skill.IsActive
                })
                .ToListAsync();

            return View(skills);
        }

        // GET: /Admin/CreateSkill
        [HttpGet]
        public IActionResult CreateSkill()
        {
            return View(new CreateSkillViewModel());
        }

        // POST: /Admin/CreateSkill
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSkill(Skill model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var name = model.Name.Trim();

            var nameExists = await _context.Skills
                .AnyAsync(skill => skill.Name == name);

            if (nameExists)
            {
                ModelState.AddModelError(nameof(model.Name), "A skill with this name already exists.");

                return View(model);
            }

            var skill = new Skill
            {
                Name = name,
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                IsActive = true
            };

            _context.Skills.Add(skill);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Skills));
        }
    }
}
