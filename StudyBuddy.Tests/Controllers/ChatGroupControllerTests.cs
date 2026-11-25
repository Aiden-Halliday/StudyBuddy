using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StudyBuddy.Data;
using StudyBuddy.Hubs;
using StudyBuddy.Models;
using StudyBuddy.Services;
using System.Net;
using System.Text.RegularExpressions;
using Xunit;
using Microsoft.AspNetCore.SignalR;
using StudyBuddy.Hubs;

namespace StudyBuddy.Tests.Controllers
{
    public class ChatGroupControllerTests
    {
        [Fact]
        public async Task Details_GroupMember_ReturnsSuccess()
        {
            using var factory = new StudyBuddyWebApplicationFactory();

            using var scope = factory.Services.CreateScope();

            var groupId = await factory.SeedChatGroupAsync();

            using var client = factory.CreateClient();

            client.DefaultRequestHeaders.Add("X-Test-User", "student-1");

            var response = await client.GetAsync($"/ChatGroup/Details/{groupId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Details_NonMember_ReturnsNotFound()
        {
            using var factory = new StudyBuddyWebApplicationFactory();

            var groupId = await factory.SeedChatGroupAsync();

            using var client = factory.CreateClient();

            client.DefaultRequestHeaders.Add("X-Test-User", "student-3");

            var response = await client.GetAsync($"/ChatGroup/Details/{groupId}");
            
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Details_NonexistentGroup_ReturnsNotFound()
        {
            using var factory = new StudyBuddyWebApplicationFactory();

            await factory.SeedChatGroupAsync();

            using var client = factory.CreateClient();

            client.DefaultRequestHeaders.Add("X-Test-User", "student-1");

            var response = await client.GetAsync("/ChatGroup/Details/9999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Details_UnauthenticatedUser_ReturnsUnauthorized()
        {
            using var factory = new StudyBuddyWebApplicationFactory();

            var groupId = await factory.SeedChatGroupAsync();

            using var client = factory.CreateClient();

            var response = await client.GetAsync($"/ChatGroup/Details/{groupId}");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }


        [Fact]
        public async Task ReportMessage_ValidReport_SavesReport()
        {
            using var factory = new StudyBuddyWebApplicationFactory();

            var groupId = await factory.SeedChatGroupAsync();
            var messageId = await factory.SeedChatMessageAsync(groupId);

            using var client = factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false
                });

            client.DefaultRequestHeaders.Add("X-Test-User", "student-1");

            var token = await GetAntiForgeryTokenAsync(client, groupId);

            var response = await client.PostAsync("/ChatGroup/ReportMessage", CreateReportForm(token, messageId, groupId, "1", "This message contains spam."));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            using var scope = factory.Services.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var report = await context.MessageReports.SingleAsync();

            Assert.Equal(messageId, report.ChatMessageId);
            Assert.Equal("student-1", report.ReporterId);
            Assert.Equal(MessageReportReason.Spam, report.Reason);
            Assert.Equal("This message contains spam.", report.Description);
            Assert.Equal(MessageReportStatus.Pending, report.Status);
        }


        [Fact]
        public async Task ReportMessage_DuplicateReport_DoesNotSaveAnotherReport()
        {
            using var factory = new StudyBuddyWebApplicationFactory();

            var groupId = await factory.SeedChatGroupAsync();
            var messageId = await factory.SeedChatMessageAsync(groupId);

            using var client = factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false
                });

            client.DefaultRequestHeaders.Add("X-Test-User", "student-1");

            var token = await GetAntiForgeryTokenAsync(client, groupId);

            var form = CreateReportForm(token, messageId, groupId, "1", "Spam.");

            var firstResponse = await client.PostAsync("/ChatGroup/ReportMessage", form);

            var secondResponse = await client.PostAsync("/ChatGroup/ReportMessage", CreateReportForm(token, messageId, groupId, "1", "Spam again."));

            Assert.Equal(HttpStatusCode.Redirect, firstResponse.StatusCode);
            Assert.Equal(HttpStatusCode.Redirect, secondResponse.StatusCode);

            using var scope = factory.Services.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var reportCount = await context.MessageReports.CountAsync();

            Assert.Equal(1, reportCount);
        }

        [Fact]
        public async Task ReportMessage_InvalidReason_DoesNotSaveReport()
        {
            using var factory = new StudyBuddyWebApplicationFactory();

            var groupId = await factory.SeedChatGroupAsync();
            var messageId = await factory.SeedChatMessageAsync(groupId);

            using var client = factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false
                });

            client.DefaultRequestHeaders.Add("X-Test-User", "student-1");

            var token = await GetAntiForgeryTokenAsync(client, groupId);

            var response = await client.PostAsync("/ChatGroup/ReportMessage", CreateReportForm(token, messageId, groupId, "999", "Invalid reason."));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            using var scope = factory.Services.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Assert.Equal(0, await context.MessageReports.CountAsync());
        }

        [Fact]
        public async Task ReportMessage_NonexistentMessage_ReturnsNotFound()
        {
            using var factory = new StudyBuddyWebApplicationFactory();

            var groupId = await factory.SeedChatGroupAsync();

            using var client = factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false
                });

            client.DefaultRequestHeaders.Add("X-Test-User", "student-1");

            var token = await GetAntiForgeryTokenAsync(client, groupId);

            var response = await client.PostAsync("/ChatGroup/ReportMessage", CreateReportForm(token, 9999, groupId, "0", "Test report."));

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            using var scope = factory.Services.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Assert.Equal(0, await context.MessageReports.CountAsync());
        }

        [Fact]
        public async Task SendMessage_SuspendedUser_DoesNotSaveMessage()
        {
            using var factory = new StudyBuddyWebApplicationFactory();

            var groupId = await factory.SeedChatGroupAsync();

            await factory.SetUserSuspensionAsync("student-1", DateTime.UtcNow.AddDays(2));

            using var client = factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false
                });

            client.DefaultRequestHeaders.Add("X-Test-User", "student-1");

            var token = await GetAntiForgeryTokenAsync(client, groupId);

            var response = await client.PostAsync("/ChatGroup/SendMessage",
                new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("__RequestVerificationToken", token),

                    new KeyValuePair<string, string>("groupId", groupId.ToString()),

                    new KeyValuePair<string, string>("content", "This message should be blocked.")
                }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            using var scope = factory.Services.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            Assert.Equal(0, await context.ChatMessages.CountAsync());
        }

        [Fact]
        public async Task SendMessage_ExpiredSuspension_AllowsMessage()
        {
            using var factory = new StudyBuddyWebApplicationFactory();

            var groupId = await factory.SeedChatGroupAsync();

            await factory.SetUserSuspensionAsync("student-1", DateTime.UtcNow.AddDays(-2));

            using var client = factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false
                });

            client.DefaultRequestHeaders.Add("X-Test-User", "student-1");

            var token = await GetAntiForgeryTokenAsync(client, groupId);

            var response = await client.PostAsync("/ChatGroup/SendMessage",
                new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("__RequestVerificationToken", token),

                    new KeyValuePair<string, string>("groupId", groupId.ToString()),

                    new KeyValuePair<string, string>("content", "My suspension has expired.")
                }));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

            using var scope = factory.Services.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var message = await context.ChatMessages.SingleAsync();

            Assert.Equal("My suspension has expired.", message.Content);
            Assert.Equal("student-1", message.SenderId);
        }


        [Fact]
        public async Task ReportingAndModerationWorkflow_DeletesMessageAndRecordsAction()
        {
            using var factory = new StudyBuddyWebApplicationFactory();

            var groupId = await factory.SeedChatGroupAsync();
            var messageId = await factory.SeedChatMessageAsync(groupId);

            using var client = factory.CreateClient(
                new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false
                });

            client.DefaultRequestHeaders.Add("X-Test-User", "student-1");

            var token = await GetAntiForgeryTokenAsync(client, groupId);

            var reportResponse = await client.PostAsync("/ChatGroup/ReportMessage",
                CreateReportForm(token, messageId, groupId, "1", "This message violates the chat rules."));

            Assert.Equal(HttpStatusCode.Redirect, reportResponse.StatusCode);

            int reportId;

            using (var scope = factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var report = await context.MessageReports.SingleAsync();

                reportId = report.MessageReportId;

                Assert.Equal(MessageReportStatus.Pending, report.Status);
            }

            using (var scope = factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var service = new ModerationService(context);

                var result = await service.DeleteMessageAsync(reportId, "student-3", "The reported message violates chat rules.");

                Assert.True(result.Succeeded, result.Message);
            }

            using (var scope = factory.Services.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                var report = await context.MessageReports.SingleAsync(r => r.MessageReportId == reportId);

                var message = await context.ChatMessages.SingleAsync(m => m.ChatMessageId == messageId);

                var action = await context.ModerationActions.SingleAsync();

                Assert.Equal(MessageReportStatus.Resolved, report.Status);

                Assert.True(message.IsDeleted);
                Assert.Equal("student-3", message.DeletedById);
                Assert.NotNull(message.DeletedUtc);

                Assert.Equal(
                    ModerationActionType.DeleteMessage,
                    action.ActionType);

                Assert.Equal(reportId, action.MessageReportId);
                Assert.Equal("student-3", action.AdminId);
                Assert.Equal("The reported message violates chat rules.", action.AdminComment);
            }
        }


        private static async Task<string> GetAntiForgeryTokenAsync(HttpClient client, int groupId)
        {
            var response = await client.GetAsync($"/ChatGroup/Details/{groupId}");

            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync();

            var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");

            Assert.True(match.Success, "The antiforgery token could not be found on the chat page.");

            return System.Net.WebUtility.HtmlDecode(match.Groups[1].Value);
        }
        private static FormUrlEncodedContent CreateReportForm(string token, int messageId, int groupId, string reason, string description)
        {
            return new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("__RequestVerificationToken", token),

                new KeyValuePair<string, string>("ChatMessageId", messageId.ToString()),

                new KeyValuePair<string, string>("ChatGroupId", groupId.ToString()),

                new KeyValuePair<string, string>("Reason", reason),

                new KeyValuePair<string, string>("Description", description)
            });
        }

    }
}