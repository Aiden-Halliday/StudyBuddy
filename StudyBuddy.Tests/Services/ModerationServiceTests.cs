using Microsoft.EntityFrameworkCore;
using StudyBuddy.Data;
using StudyBuddy.Models;
using StudyBuddy.Services;

namespace StudyBuddy.Tests.Services
{
    public class ModerationServiceTests
    {
        private static async Task<(
            ApplicationDbContext Context,
            int ReportId,
            ApplicationUser Sender,
            ApplicationUser Admin)>
            CreateTestDataAsync(bool messageDeleted = false,
                                bool senderSuspended = false)
        {
            var options =
                new DbContextOptionsBuilder<ApplicationDbContext>()
                    .UseInMemoryDatabase(Guid.NewGuid().ToString())
                    .Options;

            var context = new ApplicationDbContext(options);

            var admin = new ApplicationUser
            {
                Id = "admin-1",
                UserName = "admin",
                DisplayName = "Test Admin"
            };

            var reporter = new ApplicationUser
            {
                Id = "reporter-1",
                UserName = "reporter",
                DisplayName = "Test Reporter"
            };

            var sender = new ApplicationUser
            {
                Id = "sender-1",
                UserName = "sender",
                DisplayName = "Test Sender",
                SuspendedUntilUtc = senderSuspended
                    ? DateTime.UtcNow.AddDays(2)
                    : null
            };

            context.Users.AddRange(admin, reporter, sender);

            var group = new ChatGroup
            {
                Name = "Test Group",
                CreatedByUserId = sender.Id,
                CreatedBy = sender
            };

            var message = new ChatMessage
            {
                ChatGroup = group,
                SenderId = sender.Id,
                Sender = sender,
                Content = "Test message",
                SentUtc = DateTime.UtcNow,
                IsDeleted = messageDeleted,
                DeletedById = messageDeleted ? admin.Id : null,
                DeletedBy = messageDeleted ? admin : null,
                DeletedUtc = messageDeleted
                    ? DateTime.UtcNow
                    : null
            };

            var report = new MessageReport
            {
                ChatMessage = message,
                ReporterId = reporter.Id,
                Reporter = reporter,
                Reason = MessageReportReason.Harassment,
                Description = "Test report",
                Status = MessageReportStatus.Pending,
                CreatedUtc = DateTime.UtcNow
            };

            context.ChatGroups.Add(group);
            context.ChatMessages.Add(message);
            context.MessageReports.Add(report);

            await context.SaveChangesAsync();

            return (
                context,
                report.MessageReportId,
                sender,
                admin
            );
        }

        [Fact]
        public async Task IssueWarningAsync_ValidReport_RecordsWarning()
        {
            var data = await CreateTestDataAsync();
            using var context = data.Context;

            var service = new ModerationService(context);

            var result = await service.IssueWarningAsync(
                data.ReportId,
                data.Admin.Id,
                "A warning was issued.");

            Assert.True(result.Succeeded);
            Assert.Equal(
                MessageReportStatus.Resolved,
                (await context.MessageReports.FindAsync(data.ReportId))!.Status);

            var action = Assert.Single(context.ModerationActions);

            Assert.Equal(
                ModerationActionType.Warning,
                action.ActionType);

            Assert.Equal(
                "A warning was issued.",
                action.AdminComment);
        }

        [Fact]
        public async Task DeleteMessageAsync_ValidReport_SoftDeletesMessage()
        {
            var data = await CreateTestDataAsync();
            using var context = data.Context;

            var service = new ModerationService(context);

            var result = await service.DeleteMessageAsync(
                data.ReportId,
                data.Admin.Id,
                "The message violates chat rules.");

            Assert.True(result.Succeeded);

            var message = await context.ChatMessages
                .SingleAsync();

            Assert.True(message.IsDeleted);
            Assert.Equal(data.Admin.Id, message.DeletedById);
            Assert.NotNull(message.DeletedUtc);

            Assert.Equal(
                ModerationActionType.DeleteMessage,
                Assert.Single(context.ModerationActions).ActionType);
        }

        [Fact]
        public async Task RestoreMessageAsync_DeletedMessage_RestoresMessage()
        {
            var data = await CreateTestDataAsync(messageDeleted: true);
            using var context = data.Context;

            var service = new ModerationService(context);

            var result = await service.RestoreMessageAsync(
                data.ReportId,
                data.Admin.Id,
                "The message was reviewed and restored.");

            Assert.True(result.Succeeded);

            var message = await context.ChatMessages
                .SingleAsync();

            Assert.False(message.IsDeleted);
            Assert.Null(message.DeletedById);
            Assert.Null(message.DeletedUtc);

            Assert.Equal(
                ModerationActionType.RestoreMessage,
                Assert.Single(context.ModerationActions).ActionType);
        }

        [Fact]
        public async Task SuspendUserAsync_ValidExpiration_SuspendsSender()
        {
            var data = await CreateTestDataAsync();
            using var context = data.Context;

            var service = new ModerationService(context);
            var expiration = DateTime.UtcNow.AddDays(3);

            var result = await service.SuspendUserAsync(
                data.ReportId,
                data.Admin.Id,
                "Temporary suspension for repeated violations.",
                expiration);

            Assert.True(result.Succeeded);

            var sender = await context.Users
                .SingleAsync(u => u.Id == data.Sender.Id);

            Assert.Equal(expiration, sender.SuspendedUntilUtc);

            var action = Assert.Single(context.ModerationActions);

            Assert.Equal(
                ModerationActionType.TemporarySuspension,
                action.ActionType);

            Assert.Equal(expiration, action.ExpiresUtc);
        }

        [Fact]
        public async Task LiftSuspensionAsync_ActiveSuspension_RemovesSuspension()
        {
            var data = await CreateTestDataAsync(senderSuspended: true);
            using var context = data.Context;

            var service = new ModerationService(context);

            var result = await service.LiftSuspensionAsync(
                data.ReportId,
                data.Admin.Id,
                "The suspension has been lifted.");

            Assert.True(result.Succeeded);

            var sender = await context.Users
                .SingleAsync(u => u.Id == data.Sender.Id);

            Assert.Null(sender.SuspendedUntilUtc);

            Assert.Equal(
                ModerationActionType.LiftSuspension,
                Assert.Single(context.ModerationActions).ActionType);
        }

        [Fact]
        public async Task DismissReportAsync_ValidReport_DismissesReport()
        {
            var data = await CreateTestDataAsync();
            using var context = data.Context;

            var service = new ModerationService(context);

            var result = await service.DismissReportAsync(
                data.ReportId,
                data.Admin.Id,
                "The report does not violate community rules.");

            Assert.True(result.Succeeded);

            var report = await context.MessageReports
                .SingleAsync();

            Assert.Equal(
                MessageReportStatus.Dismissed,
                report.Status);

            Assert.Equal(
                ModerationActionType.Dismiss,
                Assert.Single(context.ModerationActions).ActionType);
        }

        [Fact]
        public async Task IssueWarningAsync_BlankComment_FailsWithoutRecordingAction()
        {
            var data = await CreateTestDataAsync();
            using var context = data.Context;

            var service = new ModerationService(context);

            var result = await service.IssueWarningAsync(
                data.ReportId,
                data.Admin.Id,
                "   ");

            Assert.False(result.Succeeded);
            Assert.Contains("comment", result.Message);
            Assert.Empty(context.ModerationActions);
        }

        [Fact]
        public async Task DeleteMessageAsync_NonexistentReport_Fails()
        {
            var data = await CreateTestDataAsync();
            using var context = data.Context;

            var service = new ModerationService(context);

            var result = await service.DeleteMessageAsync(
                99999,
                data.Admin.Id,
                "Testing a missing report.");

            Assert.False(result.Succeeded);
            Assert.Empty(context.ModerationActions);
        }

        [Fact]
        public async Task SuspendUserAsync_ExpirationInPast_Fails()
        {
            var data = await CreateTestDataAsync();
            using var context = data.Context;

            var service = new ModerationService(context);

            var result = await service.SuspendUserAsync(
                data.ReportId,
                data.Admin.Id,
                "Testing an invalid expiration.",
                DateTime.UtcNow.AddDays(-1));

            Assert.False(result.Succeeded);
            Assert.Null(data.Sender.SuspendedUntilUtc);
            Assert.Empty(context.ModerationActions);
        }
    }
}