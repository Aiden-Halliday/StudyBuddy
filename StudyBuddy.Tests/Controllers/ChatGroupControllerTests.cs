using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace StudyBuddy.Tests.Controllers
{
    public class ChatGroupControllerTests
    {
        [Fact]
        public async Task Details_GroupMember_ReturnsSuccess()
        {
            using var factory = new StudyBuddyWebApplicationFactory();

            var groupId = await factory.SeedChatGroupAsync();

            using var client = factory.CreateClient();

            client.DefaultRequestHeaders.Add(
                "X-Test-User", "student-1");

            var response = await client.GetAsync(
                $"/ChatGroup/Details/{groupId}");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task Details_NonMember_ReturnsNotFound()
        {
            using var factory = new StudyBuddyWebApplicationFactory();

            var groupId = await factory.SeedChatGroupAsync();

            using var client = factory.CreateClient();

            client.DefaultRequestHeaders.Add(
                "X-Test-User", "student-3");

            var response = await client.GetAsync(
                $"/ChatGroup/Details/{groupId}");
            
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Details_NonexistentGroup_ReturnsNotFound()
        {
            using var factory = new StudyBuddyWebApplicationFactory();

            await factory.SeedChatGroupAsync();

            using var client = factory.CreateClient();

            client.DefaultRequestHeaders.Add(
                "X-Test-User", "student-1");

            var response = await client.GetAsync(
                "/ChatGroup/Details/9999");

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task Details_UnauthenticatedUser_ReturnsUnauthorized()
        {
            using var factory = new StudyBuddyWebApplicationFactory();

            var groupId = await factory.SeedChatGroupAsync();

            using var client = factory.CreateClient();

            var response = await client.GetAsync(
                $"/ChatGroup/Details/{groupId}");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }
}