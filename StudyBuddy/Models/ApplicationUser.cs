using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace StudyBuddy.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [StringLength(50)]
        public string DisplayName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Bio { get; set; }

        // Cloudflare R2 object key
        [StringLength(500)]
        public string? ProfileImageKey { get; set; }

        // Account timestamps
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedUtc { get; set; }
    }
}
