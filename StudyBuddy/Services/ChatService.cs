using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.Models;

namespace StudyBuddy.Services
{
    public class ChatService : IChatService
    {
        private const long MaxFileSize = 10 * 1024 * 1024;

        private readonly ApplicationDbContext _context;
        private readonly CloudflareR2Service? _r2Service;

        public ChatService(ApplicationDbContext context)
        {
            _context = context;
        }

        public ChatService(ApplicationDbContext context, CloudflareR2Service r2Service)
        {
            _context = context;
            _r2Service = r2Service;
        }

        public async Task<bool> IsMemberAsync(int groupId, string userId)
        {
            return await _context.ChatGroupMembers
                .AnyAsync(m => m.ChatGroupId == groupId &&
                               m.UserId == userId);
        }

        // Existing method for regular text messages.
        public async Task<ChatMessage> SendMessageAsync( int groupId, string userId, string content)
        {
            content = content?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new ArgumentException(
                    "Message cannot be empty.");
            }

            if (content.Length > 2000)
            {
                throw new ArgumentException(
                    "Message cannot exceed 2000 characters.");
            }

            var isMember = await IsMemberAsync(groupId, userId);

            if (!isMember)
            {
                throw new UnauthorizedAccessException(
                    "You are not a member of this chat group.");
            }

            var message = new ChatMessage
            {
                ChatGroupId = groupId,
                SenderId = userId,
                Content = content,
                SentUtc = DateTime.UtcNow
            };

            _context.ChatMessages.Add(message);

            await _context.SaveChangesAsync();

            await _context.Entry(message).Reference(m => m.Sender).LoadAsync();

            return message;
        }

        // New method for messages with multiple attachments.
        public async Task<ChatMessage> SendMessageAsync(int groupId, string userId, string content, IReadOnlyCollection<IFormFile> attachments)
        {
            content = content?.Trim() ?? string.Empty;

            if (attachments == null)
            {
                throw new ArgumentNullException(nameof(attachments));
            }

            if (string.IsNullOrWhiteSpace(content) && attachments.Count == 0)
            {
                throw new ArgumentException(
                    "A message or attachment is required.");
            }

            if (content.Length > 2000)
            {
                throw new ArgumentException(
                    "Message cannot exceed 2000 characters.");
            }

            var isMember = await IsMemberAsync(groupId, userId);

            if (!isMember)
            {
                throw new UnauthorizedAccessException(
                    "You are not a member of this chat group.");
            }

            if (attachments.Count > 0 && _r2Service == null)
            {
                throw new InvalidOperationException(
                    "Cloudflare R2 service is not configured.");
            }

            // Validate every file before uploading anything.
            var validatedFiles = new List<(IFormFile File, string FileName, string ContentType, string Extension)>();

            foreach (var file in attachments)
            {
                if (file == null || file.Length == 0)
                {
                    throw new ArgumentException("Uploaded files cannot be empty.");
                }

                if (file.Length > MaxFileSize)
                {
                    throw new ArgumentException("Each attachment cannot exceed 10 MB.");
                }

                var fileName = Path.GetFileName(file.FileName.Replace('\\', '/'));

                if (string.IsNullOrWhiteSpace(fileName))
                {
                    throw new ArgumentException("Invalid filename.");
                }

                if (fileName.Length > 255)
                {
                    throw new ArgumentException("Filename cannot exceed 255 characters.");
                }

                var extension = Path.GetExtension(fileName).ToLowerInvariant();

                string contentType;

                // Validate actual file contents rather than trusting
                // the browser-provided ContentType.
                using (var stream = file.OpenReadStream())
                {
                    var header = new byte[12];
                    var bytesRead = 0;

                    while (bytesRead < header.Length)
                    {
                        var read = await stream.ReadAsync(
                            header.AsMemory(bytesRead));

                        if (read == 0)
                            break;

                        bytesRead += read;
                    }

                    contentType = GetContentType(header, bytesRead, extension);
                }

                validatedFiles.Add((file, fileName, contentType, extension));
            }

            var message = new ChatMessage
            {
                ChatGroupId = groupId,
                SenderId = userId,
                Content = content,
                SentUtc = DateTime.UtcNow
            };

            var uploadedKeys = new List<string>();

            try
            {
                foreach (var validatedFile in validatedFiles)
                {
                    var storageKey = $"chat-attachments/{groupId}/{Guid.NewGuid():N}" + validatedFile.Extension;

                    using var stream = validatedFile.File.OpenReadStream();

                    await _r2Service!.UploadAsync(stream, storageKey, validatedFile.ContentType);

                    uploadedKeys.Add(storageKey);

                    message.Attachments.Add(new ChatAttachment
                    {
                        FileName = validatedFile.FileName,
                        StorageKey = storageKey,
                        ContentType = validatedFile.ContentType,
                        FileSize = validatedFile.File.Length,
                        UploadedUtc = DateTime.UtcNow
                    });
                }

                _context.ChatMessages.Add(message);

                await _context.SaveChangesAsync();
            }
            catch
            {
                // Clean up files already uploaded if a later upload
                // or database save fails.
                foreach (var key in uploadedKeys)
                {
                    try
                    {
                        await _r2Service!.DeleteAsync(key);
                    }
                    catch
                    {
                        // Preserve the original exception.
                        // Failed cleanup may require manual R2 cleanup.
                    }
                }

                throw;
            }

            await _context.Entry(message).Reference(m => m.Sender).LoadAsync();

            return message;
        }

        private static string GetContentType(
            byte[] header,
            int bytesRead,
            string extension)
        {
            // JPEG
            if (bytesRead >= 3 &&
                header[0] == 0xFF &&
                header[1] == 0xD8 &&
                header[2] == 0xFF &&
                (extension == ".jpg" || extension == ".jpeg"))
            {
                return "image/jpeg";
            }

            // PNG
            if (bytesRead >= 8 &&
                header[0] == 0x89 &&
                header[1] == 0x50 &&
                header[2] == 0x4E &&
                header[3] == 0x47 &&
                header[4] == 0x0D &&
                header[5] == 0x0A &&
                header[6] == 0x1A &&
                header[7] == 0x0A &&
                extension == ".png")
            {
                return "image/png";
            }

            // WebP
            if (bytesRead >= 12 &&
                header[0] == 'R' &&
                header[1] == 'I' &&
                header[2] == 'F' &&
                header[3] == 'F' &&
                header[8] == 'W' &&
                header[9] == 'E' &&
                header[10] == 'B' &&
                header[11] == 'P' &&
                extension == ".webp")
            {
                return "image/webp";
            }

            // PDF
            if (bytesRead >= 5 &&
                header[0] == '%' &&
                header[1] == 'P' &&
                header[2] == 'D' &&
                header[3] == 'F' &&
                header[4] == '-' &&
                extension == ".pdf")
            {
                return "application/pdf";
            }

            throw new ArgumentException("Unsupported file type or file contents do not match the filename. " + "Only JPEG, PNG, WebP, and PDF files are allowed.");
        }
    }
}
