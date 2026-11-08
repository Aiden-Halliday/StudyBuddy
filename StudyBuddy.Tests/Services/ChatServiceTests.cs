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
    }
}