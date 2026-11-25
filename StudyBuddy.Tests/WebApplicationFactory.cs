using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudyBuddy.Data;
using StudyBuddy.Models;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace StudyBuddy.Tests
{
    public class TestAuthHandler
        : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Test-User", out var userId))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, userId.ToString())
            };

            var identity = new ClaimsIdentity(claims, "Test");
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, "Test");

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    public class StudyBuddyWebApplicationFactory
        : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection;

        public StudyBuddyWebApplicationFactory()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((context, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["CloudflareR2:BucketName"] = "test-bucket",
                    ["CloudflareR2:AccessKeyId"] = "test-access-key",
                    ["CloudflareR2:SecretAccessKey"] = "test-secret-key",
                    ["CloudflareR2:ServiceUrl"] = "https://example.r2.cloudflarestorage.com"
                });
            });

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlite(_connection));

                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "Test";
                    options.DefaultChallengeScheme = "Test";
                    options.DefaultForbidScheme = "Test";
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>("Test", options => { });
            });
        }

        public async Task<int> SeedChatGroupAsync()
        {
            using var scope = Services.CreateScope();

            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            await context.Database.EnsureCreatedAsync();

            var student1 = new ApplicationUser
            {
                Id = "student-1",
                UserName = "student1",
                DisplayName = "Student One"
            };

            var student2 = new ApplicationUser
            {
                Id = "student-2",
                UserName = "student2",
                DisplayName = "Student Two"
            };

            var student3 = new ApplicationUser
            {
                Id = "student-3",
                UserName = "student3",
                DisplayName = "Student Three"
            };

            context.Users.AddRange(student1, student2, student3);

            var group = new ChatGroup
            {
                Name = "Test Group",
                CreatedByUserId = student1.Id
            };

            context.ChatGroups.Add(group);

            await context.SaveChangesAsync();

            context.ChatGroupMembers.AddRange(
                new ChatGroupMember
                {
                    ChatGroupId = group.ChatGroupId,
                    UserId = student1.Id
                },
                new ChatGroupMember
                {
                    ChatGroupId = group.ChatGroupId,
                    UserId = student2.Id
                });

            await context.SaveChangesAsync();

            return group.ChatGroupId;
        }

        public async Task SetUserSuspensionAsync(
            string userId,
            DateTime? suspendedUntilUtc)
        {
            using var scope = Services.CreateScope();

            var context = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            var user = await context.Users
                .SingleAsync(u => u.Id == userId);

            user.SuspendedUntilUtc = suspendedUntilUtc;

            await context.SaveChangesAsync();
        }
        public async Task<int> SeedChatMessageAsync(int groupId)
        {
            using var scope = Services.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var message = new ChatMessage
            {
                ChatGroupId = groupId,
                SenderId = "student-2",
                Content = "This is a test chat message.",
                SentUtc = DateTime.UtcNow
            };

            context.ChatMessages.Add(message);

            await context.SaveChangesAsync();

            return message.ChatMessageId;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                _connection.Dispose();
            }
        }
    }
}