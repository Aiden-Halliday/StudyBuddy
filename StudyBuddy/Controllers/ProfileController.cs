using Amazon.S3;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.Models;
using StudyBuddy.Services;
using StudyBuddy.ViewModels.Profile;
using System.Net;

namespace StudyBuddy.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private const long MaxProfileImageSize = 5 * 1024 * 1024; // This is 5 MB

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly CloudflareR2Service _r2Service;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        CloudflareR2Service r2Service,
        ILogger<ProfileController> logger)
    {
        _userManager = userManager;
        _context = context;
        _r2Service = r2Service;
        _logger = logger;
    }

    // GET: /Profile
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var model = new ProfileViewModel
        {
            UserId = user.Id,
            DisplayName = user.DisplayName,
            Bio = user.Bio,
            ProfileImageKey = user.ProfileImageKey,
            CreatedUtc = user.CreatedUtc,
            Skills = await _context.UserSkills
                .Include(us => us.Skill)
                .Where(us => us.UserId == user.Id)
                .OrderBy(us => us.Skill.Name)
                .ToListAsync(),
            AvailableSkills = await _context.Skills
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync()
        };

        return View(model);
    }

    // POST: /Profile/UploadProfileImage
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadProfileImage(
        IFormFile? profileImage,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        if (profileImage == null || profileImage.Length == 0)
        {
            TempData["ErrorMessage"] = "Please select an image to upload.";
            return RedirectToAction(nameof(Index));
        }

        if (profileImage.Length > MaxProfileImageSize)
        {
            TempData["ErrorMessage"] = "Your profile picture must be 5 MB or smaller.";
            return RedirectToAction(nameof(Index));
        }

        var extension = await ValidateImageAsync(
            profileImage,
            cancellationToken);

        if (extension == null)
        {
            TempData["ErrorMessage"] =
                "Please upload a valid JPEG, PNG, or WebP image.";
            return RedirectToAction(nameof(Index));
        }

        var contentType = extension switch
        {
            ".jpg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => throw new InvalidOperationException("Unsupported image type.")
        };

        // Will never use the original filename as the R2 object key.
        var newObjectKey =
            $"profile-images/{user.Id}/{Guid.NewGuid():N}{extension}";

        var previousObjectKey = user.ProfileImageKey;

        // Will upload the new image before changing the database record.
        await using (var imageStream = profileImage.OpenReadStream())
        {
            await _r2Service.UploadAsync(
                imageStream,
                newObjectKey,
                contentType,
                cancellationToken);
        }

        user.ProfileImageKey = newObjectKey;
        user.UpdatedUtc = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // In case the database update fails, remove the newly uploaded image.
            try
            {
                await _r2Service.DeleteAsync(
                    newObjectKey,
                    CancellationToken.None);
            }
            catch (Exception cleanupException)
            {
                _logger.LogError(
                    cleanupException,
                    "Could not clean up an uploaded profile image for user {UserId}.",
                    user.Id);
            }

            throw;
        }

        // Delete the previous image only after the database update succeeds.
        if (!string.IsNullOrWhiteSpace(previousObjectKey))
        {
            try
            {
                await _r2Service.DeleteAsync(
                    previousObjectKey,
                    cancellationToken);
            }
            catch (Exception deleteException)
            {
                _logger.LogWarning(
                    deleteException,
                    "Could not delete the previous profile image for user {UserId}.",
                    user.Id);
            }
        }

        TempData["SuccessMessage"] =
            "Your profile picture was updated successfully.";

        return RedirectToAction(nameof(Index));
    }

    // GET: /Profile/Image/{userId}
    [HttpGet]
    public async Task<IActionResult> Image(
        string userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return NotFound();

        var user = await _userManager.FindByIdAsync(userId);

        if (user == null ||
            string.IsNullOrWhiteSpace(user.ProfileImageKey))
        {
            return NotFound();
        }

        try
        {
            using var response = await _r2Service.GetObjectAsync(
                user.ProfileImageKey,
                cancellationToken);

            var memoryStream = new MemoryStream();

            await response.ResponseStream.CopyToAsync(
                memoryStream,
                cancellationToken);

            memoryStream.Position = 0;

            var contentType = response.Headers.ContentType;

            if (string.IsNullOrWhiteSpace(contentType) ||
                !contentType.StartsWith(
                    "image/",
                    StringComparison.OrdinalIgnoreCase))
            {
                memoryStream.Dispose();
                return NotFound();
            }

            return File(memoryStream, contentType);
        }
        catch (AmazonS3Exception ex)
            when (ex.StatusCode == HttpStatusCode.NotFound ||
                  ex.ErrorCode == "NoSuchKey")
        {
            return NotFound();
        }
        catch (AmazonS3Exception ex)
        {
            _logger.LogError(
                ex,
                "Could not retrieve the profile image for user {UserId}.",
                userId);

            return StatusCode(StatusCodes.Status500InternalServerError);
        }
    }

    private static async Task<string?> ValidateImageAsync(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName)
            .ToLowerInvariant();

        var expectedContentType = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => null
        };

        if (expectedContentType == null ||
            !string.Equals(
                file.ContentType,
                expectedContentType,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var header = new byte[12];

        await using var stream = file.OpenReadStream();

        var bytesRead = await stream.ReadAsync(
            header.AsMemory(0, header.Length),
            cancellationToken);

        bool isValid = extension switch
        {
            ".jpg" or ".jpeg" =>
                bytesRead >= 3 &&
                header[0] == 0xFF &&
                header[1] == 0xD8 &&
                header[2] == 0xFF,

            ".png" =>
                bytesRead >= 8 &&
                header[0] == 0x89 &&
                header[1] == 0x50 &&
                header[2] == 0x4E &&
                header[3] == 0x47 &&
                header[4] == 0x0D &&
                header[5] == 0x0A &&
                header[6] == 0x1A &&
                header[7] == 0x0A,

            ".webp" =>
                bytesRead >= 12 &&
                header[0] == 'R' &&
                header[1] == 'I' &&
                header[2] == 'F' &&
                header[3] == 'F' &&
                header[8] == 'W' &&
                header[9] == 'E' &&
                header[10] == 'B' &&
                header[11] == 'P',

            _ => false
        };

        if (!isValid)
            return null;

        return extension == ".jpeg" ? ".jpg" : extension;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSkill(ProfileViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        if (!Enum.IsDefined(typeof(SkillType), model.Type))
            return BadRequest();

        var skill = await _context.Skills.FirstOrDefaultAsync(s =>
            s.SkillId == model.SelectedSkillId && s.IsActive);

        if (skill == null)
        {
            TempData["ErrorMessage"] = "Please select an available skill.";
            return RedirectToAction(nameof(Index));
        }

        var alreadyAdded = await _context.UserSkills.AnyAsync(us =>
            us.UserId == user.Id && us.SkillId == skill.SkillId);

        if (alreadyAdded)
        {
            TempData["ErrorMessage"] = "You have already added this skill.";
            return RedirectToAction(nameof(Index));
        }

        var userSkill = new UserSkill
        {
            UserId = user.Id,
            SkillId = skill.SkillId,
            Type = model.Type
        };

        _context.UserSkills.Add(userSkill);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Skill added successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSkill(int userSkillId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return NotFound();

        var userSkill = await _context.UserSkills.FirstOrDefaultAsync(us =>
            us.UserSkillId == userSkillId && us.UserId == user.Id);

        if (userSkill == null) return NotFound();

        _context.UserSkills.Remove(userSkill);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Skill removed successfully.";
        return RedirectToAction(nameof(Index));
    }
}
