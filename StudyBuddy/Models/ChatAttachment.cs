using System.ComponentModel.DataAnnotations;

namespace StudyBuddy.Models
{
    public class ChatAttachment
    {
        public int ChatAttachmentId { get; set; }

        // Associated chat message
        public int ChatMessageId { get; set; }
        public ChatMessage ChatMessage { get; set; } = null!;

        // Original uploaded filename
        [Required]
        [StringLength(255)]
        public string FileName { get; set; } = string.Empty;

        // Cloudflare R2 object key, not a public URL
        [Required]
        [StringLength(500)]
        public string StorageKey { get; set; } = string.Empty;

        // MIME type, such as image/png or application/pdf
        [Required]
        [StringLength(100)]
        public string ContentType { get; set; } = string.Empty;

        // File size in bytes
        public long FileSize { get; set; }

        public DateTime UploadedUtc { get; set; } = DateTime.UtcNow;
    }
}
