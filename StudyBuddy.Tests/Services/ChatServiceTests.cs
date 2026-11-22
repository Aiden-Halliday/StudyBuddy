using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.Models;
using StudyBuddy.Services;
using Xunit;

namespace StudyBuddy.Tests
{
    public class ChatServiceTests
    {
        private ApplicationDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private async Task<(ChatGroup group, ApplicationUser user)>
            CreateGroupWithMemberAsync(ApplicationDbContext context)
        {
            var user = new ApplicationUser
            {
                Id = "user-1",
                UserName = "student1",
                DisplayName = "Student One"
            };

            context.Users.Add(user);

            var group = new ChatGroup
            {
                Name = "Test Group",
                CreatedByUserId = user.Id,
                CreatedBy = user
            };

            context.ChatGroups.Add(group);

            context.ChatGroupMembers.Add(new ChatGroupMember
            {
                ChatGroup = group,
                UserId = user.Id,
                User = user
            });

            await context.SaveChangesAsync();

            return (group, user);
        }

        [Fact]
        public async Task SendMessageAsync_ValidMessage_SavesMessage()
        {
            using var context = CreateContext();

            var (group, user) = await CreateGroupWithMemberAsync(context);
            var service = new ChatService(context);

            var message = await service.SendMessageAsync(
                group.ChatGroupId, user.Id, "Hello everyone!");

            Assert.NotNull(message);
            Assert.Equal("Hello everyone!", message.Content);
            Assert.Equal(group.ChatGroupId, message.ChatGroupId);
            Assert.Equal(user.Id, message.SenderId);
            Assert.Equal(1, await context.ChatMessages.CountAsync());
        }

        [Fact]
        public async Task SendMessageAsync_MessageWithWhitespace_TrimsContent()
        {
            using var context = CreateContext();

            var (group, user) = await CreateGroupWithMemberAsync(context);
            var service = new ChatService(context);

            var message = await service.SendMessageAsync(
                group.ChatGroupId, user.Id, "  Hello everyone!  ");

            Assert.Equal("Hello everyone!", message.Content);
        }

        [Fact]
        public async Task SendMessageAsync_EmptyMessage_ThrowsArgumentException()
        {
            using var context = CreateContext();

            var (group, user) = await CreateGroupWithMemberAsync(context);
            var service = new ChatService(context);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.SendMessageAsync(group.ChatGroupId, user.Id, ""));
        }

        [Fact]
        public async Task SendMessageAsync_WhitespaceMessage_ThrowsArgumentException()
        {
            using var context = CreateContext();

            var (group, user) = await CreateGroupWithMemberAsync(context);
            var service = new ChatService(context);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.SendMessageAsync(group.ChatGroupId, user.Id, "   "));
        }

        [Fact]
        public async Task SendMessageAsync_MessageOver2000Characters_ThrowsArgumentException()
        {
            using var context = CreateContext();

            var (group, user) = await CreateGroupWithMemberAsync(context);
            var service = new ChatService(context);

            var content = new string('a', 2001);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.SendMessageAsync(group.ChatGroupId, user.Id, content));
        }

        [Fact]
        public async Task SendMessageAsync_UserNotMember_ThrowsUnauthorizedAccessException()
        {
            using var context = CreateContext();

            var (group, _) = await CreateGroupWithMemberAsync(context);
            var service = new ChatService(context);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.SendMessageAsync(group.ChatGroupId, "not-a-member", "Hello!"));

            Assert.Equal(0, await context.ChatMessages.CountAsync());
        }

        [Fact]
        public async Task SendMessageAsync_ValidMessage_LoadsSender()
        {
            using var context = CreateContext();

            var (group, user) = await CreateGroupWithMemberAsync(context);
            var service = new ChatService(context);

            var message = await service.SendMessageAsync(
                group.ChatGroupId, user.Id, "Testing sender");

            Assert.NotNull(message.Sender);
            Assert.Equal("Student One", message.Sender.DisplayName);
        }

        [Fact]
        public async Task SendMessageAsync_EmptyContentAndNoAttachments_ThrowsArgumentException()
        {
            using var context = CreateContext();

            var (group, user) = await CreateGroupWithMemberAsync(context);
            var service = new ChatService(context);

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.SendMessageAsync(
                    group.ChatGroupId,
                    user.Id,
                    "",
                    Array.Empty<IFormFile>()));

            Assert.Equal(0, await context.ChatMessages.CountAsync());
        }

        [Fact]
        public async Task SendMessageAsync_AttachmentOnlyWithoutR2_ThrowsInvalidOperationException()
        {
            using var context = CreateContext();

            var (group, user) = await CreateGroupWithMemberAsync(context);
            var service = new ChatService(context);

            var file = CreateTestFile("test.png", "image/png");

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.SendMessageAsync(
                    group.ChatGroupId,
                    user.Id,
                    "",
                    new List<IFormFile> { file }));

            Assert.Equal(0, await context.ChatMessages.CountAsync());
        }

        [Fact]
        public async Task SendMessageAsync_AttachmentFromNonMember_ThrowsUnauthorizedAccessException()
        {
            using var context = CreateContext();

            var (group, _) = await CreateGroupWithMemberAsync(context);
            var service = new ChatService(context);

            var file = CreateTestFile("test.png", "image/png");

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.SendMessageAsync(
                    group.ChatGroupId,
                    "not-a-member",
                    "",
                    new List<IFormFile> { file }));

            Assert.Equal(0, await context.ChatMessages.CountAsync());
        }

        [Fact]
        public async Task SendMessageAsync_MessageOver2000CharactersWithAttachment_ThrowsArgumentException()
        {
            using var context = CreateContext();

            var (group, user) = await CreateGroupWithMemberAsync(context);
            var service = new ChatService(context);

            var content = new string('a', 2001);
            var file = CreateTestFile("test.png", "image/png");

            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.SendMessageAsync(
                    group.ChatGroupId,
                    user.Id,
                    content,
                    new List<IFormFile> { file }));

            Assert.Equal(0, await context.ChatMessages.CountAsync());
        }

        private IFormFile CreateTestFile(string fileName, string contentType)
        {
            byte[] content;

            if (fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                content = new byte[]
                {
                    0x89, 0x50, 0x4E, 0x47,
                    0x0D, 0x0A, 0x1A, 0x0A,
                    0x00, 0x00, 0x00, 0x00
                };
            }
            else if (fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                content = System.Text.Encoding.ASCII.GetBytes(
                    "%PDF-1.7 test document");
            }
            else
            {
                throw new ArgumentException("Unsupported test file.");
            }

            var stream = new MemoryStream(content);

            return new FormFile(stream, 0, stream.Length, "attachments", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
        }
    }
}