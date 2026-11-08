using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StudyBuddy.Data;
using StudyBuddy.Models;
using StudyBuddy.Services;

namespace StudyBuddy.Tests.Integration;

// Uses real MVC filters, Razor views, password login and cookie validation.
// Every test gets a private SQLite database, not your SQL Server database.
public class AdminAccessTests : IDisposable
{
    private const string Password = "Test-Pass123!";

    private readonly AdminFactory _factory = new();
    private readonly HttpClient _admin;
    private readonly HttpClient _member;
    private readonly string _adminId;
    private readonly string _memberId;

    public AdminAccessTests()
    {
        _admin = Client();
        _member = Client();

        using var scope = _factory.Services.CreateScope();

        var db =
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.Database.EnsureCreated();

        var roles = scope.ServiceProvider
            .GetRequiredService<RoleManager<IdentityRole>>();

        Check(
            roles.CreateAsync(new IdentityRole("Admin"))
                .GetAwaiter()
                .GetResult());

        var users = scope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();

        var admin = new ApplicationUser
        {
            Email = "admin@example.com",
            UserName = "admin@example.com",
            DisplayName = "Admin"
        };

        var member = new ApplicationUser
        {
            Email = "member@example.com",
            UserName = "member@example.com",
            DisplayName = "Member"
        };

        Check(users.CreateAsync(admin, Password).GetAwaiter().GetResult());
        Check(users.CreateAsync(member, Password).GetAwaiter().GetResult());
        Check(users.AddToRoleAsync(admin, "Admin").GetAwaiter().GetResult());

        _adminId = admin.Id;
        _memberId = member.Id;
    }

    [Theory]
    [InlineData("/Admin")]
    [InlineData("/Admin/Skills")]
    [InlineData("/Admin/CreateSkill")]
    [InlineData("/Admin/Delete?id=someone")]
    public async Task Anonymous_IsSentToLogin(string path)
    {
        using var client = Client();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        Assert.StartsWith(
            "https://localhost/Account/Login",
            response.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("/Admin")]
    [InlineData("/Admin/Skills")]
    [InlineData("/Admin/CreateSkill")]
    [InlineData("/Admin/Delete?id=someone")]
    public async Task Member_IsDeniedEvenUsingDirectUrl(string path)
    {
        await Login(_member, "member@example.com");

        var response = await _member.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        Assert.StartsWith(
            "https://localhost/Account/AccessDenied",
            response.Headers.Location!.ToString());

        var denied = await _member.GetAsync(response.Headers.Location);

        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Contains(
            "Access denied",
            await denied.Content.ReadAsStringAsync());

        var home = await _member.GetStringAsync("/");

        Assert.DoesNotContain(">User Accounts</a>", home);
    }

    [Fact]
    public async Task AdminViews_Render_AndSearchWorks()
    {
        await Login(_admin, "admin@example.com");

        foreach (var path in new[]
        {
            "/Admin",
            "/Admin/Skills",
            "/Admin/CreateSkill",
            $"/Admin/Delete/{_memberId}"
        })
        {
            Assert.Equal(
                HttpStatusCode.OK,
                (await _admin.GetAsync(path)).StatusCode);
        }

        var html = await _admin.GetStringAsync(
            "/Admin?search=member%40example.com");

        Assert.Contains("member@example.com", html);
        Assert.DoesNotContain("admin@example.com", html);
    }

    [Theory]
    [InlineData("Disable")]
    [InlineData("Enable")]
    [InlineData("GrantAdmin")]
    [InlineData("RemoveAdmin")]
    [InlineData("Delete")]
    [InlineData("CreateSkill")]
    public async Task Changes_RequireAntiforgeryToken(string action)
    {
        await Login(_admin, "admin@example.com");

        var response = await _admin.PostAsync(
            "/Admin/" + action,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["id"] = _memberId
            }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        Assert.False(await Read(async (db, users) =>
            AdminAccountStatus.IsDisabled(
                (await users.FindByIdAsync(_memberId))!)));
    }

    [Theory]
    [InlineData("Disable")]
    [InlineData("Enable")]
    [InlineData("GrantAdmin")]
    [InlineData("RemoveAdmin")]
    [InlineData("Delete")]
    [InlineData("CreateSkill")]
    public async Task Member_CannotPostAdminActions_WithValidToken(
        string action)
    {
        await Login(_member, "member@example.com");

        var response = await Post(
            _member,
            "/Admin/" + action,
            "/Account/Login",
            new()
            {
                ["id"] = _memberId,
                ["Confirmation"] = "member@example.com",
                ["Name"] = "Forbidden skill"
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        Assert.Contains(
            "/Account/AccessDenied",
            response.Headers.Location!.ToString());

        Assert.True(await Read(async (db, users) =>
            await users.FindByIdAsync(_memberId) != null &&
            !await db.Skills.AnyAsync()));
    }

    [Theory]
    [InlineData("Disable")]
    [InlineData("Enable")]
    [InlineData("GrantAdmin")]
    [InlineData("RemoveAdmin")]
    public async Task Mutations_AreNotAvailableThroughGet(string action)
    {
        await Login(_admin, "admin@example.com");

        Assert.Equal(
            HttpStatusCode.MethodNotAllowed,
            (await _admin.GetAsync(
                $"/Admin/{action}/{_memberId}")).StatusCode);
    }

    [Fact]
    public async Task Disable_RejectsExistingCookie_AndEnableAllowsLoginAgain()
    {
        await Login(_admin, "admin@example.com");
        await Login(_member, "member@example.com");

        Assert.Equal(
            HttpStatusCode.OK,
            (await _member.GetAsync("/Profile")).StatusCode);

        await Change("Disable", _memberId);

        Assert.True(await Read(async (db, users) =>
            AdminAccountStatus.IsDisabled(
                (await users.FindByIdAsync(_memberId))!)));

        Assert.Equal(
            HttpStatusCode.Redirect,
            (await _member.GetAsync("/Profile")).StatusCode);

        Assert.Equal(
            HttpStatusCode.OK,
            (await Login(
                _member,
                "member@example.com",
                expectSuccess: false)).StatusCode);

        await Change("Enable", _memberId);

        await Login(_member, "member@example.com");

        Assert.Equal(
            HttpStatusCode.OK,
            (await _member.GetAsync("/Profile")).StatusCode);
    }

    [Fact]
    public async Task RoleChanges_RefreshAlreadySignedInUsers_OnNextRequest()
    {
        await Login(_admin, "admin@example.com");
        await Login(_member, "member@example.com");

        await Change("GrantAdmin", _memberId);

        Assert.Equal(
            HttpStatusCode.OK,
            (await _member.GetAsync("/Admin")).StatusCode);

        await Change("RemoveAdmin", _memberId);

        var denied = await _member.GetAsync("/Admin");

        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);

        Assert.Contains(
            "/Account/AccessDenied",
            denied.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("Disable")]
    [InlineData("RemoveAdmin")]
    [InlineData("Delete")]
    public async Task Admin_CannotChangeOwnAccount(string action)
    {
        await Login(_admin, "admin@example.com");

        await Change(action, _adminId, "admin@example.com");

        Assert.True(await Read(async (db, users) =>
        {
            var user = await users.FindByIdAsync(_adminId);

            return user != null &&
                await users.IsInRoleAsync(user, "Admin") &&
                !AdminAccountStatus.IsLockedOut(user);
        }));

        Assert.Contains(
            "cannot change your own account",
            await _admin.GetStringAsync("/Admin"));
    }

    [Fact]
    public async Task InvalidSkill_ReRendersCorrectModel_AndDuplicateIsRejected()
    {
        await Login(_admin, "admin@example.com");

        var invalid = await Post(
            _admin,
            "/Admin/CreateSkill",
            "/Admin/CreateSkill",
            new() { ["Name"] = "" });

        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);

        Assert.Contains(
            "required",
            await invalid.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);

        var created = await Post(
            _admin,
            "/Admin/CreateSkill",
            "/Admin/CreateSkill",
            new()
            {
                ["Name"] = "  Calculus  ",
                ["Description"] = "  Limits  ",
                ["IsActive"] = "false"
            });

        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);

        var duplicate = await Post(
            _admin,
            "/Admin/CreateSkill",
            "/Admin/CreateSkill",
            new() { ["Name"] = "calculus" });

        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);

        Assert.Contains(
            "already exists",
            await duplicate.Content.ReadAsStringAsync());

        Assert.True(await Read(async (db, users) =>
        {
            var skill = await db.Skills.SingleAsync();

            return skill.Name == "Calculus" &&
                skill.Description == "Limits" &&
                skill.IsActive;
        }));
    }

    [Fact]
    public async Task Delete_RequiresTypedConfirmation_AndGetDoesNotDelete()
    {
        await Login(_admin, "admin@example.com");

        await _admin.GetAsync($"/Admin/Delete/{_memberId}");
        await Change("Delete", _memberId, "wrong@example.com");

        Assert.True(await Read(async (db, users) =>
            await users.FindByIdAsync(_memberId) != null));

        Assert.Contains(
            "confirmation must match",
            await _admin.GetStringAsync("/Admin"));
    }

    [Fact]
    public async Task Delete_CleansRelatedRows_PreservesOtherGroups_AndRevokesCookie()
    {
        await Login(_admin, "admin@example.com");
        await Login(_member, "member@example.com");

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            var owned = new ChatGroup
            {
                Name = "Member owned",
                CreatedByUserId = _memberId
            };

            var other = new ChatGroup
            {
                Name = "Admin owned",
                CreatedByUserId = _adminId
            };

            db.ChatGroups.AddRange(owned, other);
            db.Skills.Add(new Skill { Name = "Math" });

            await db.SaveChangesAsync();

            db.ChatGroupMembers.AddRange(
                new ChatGroupMember
                {
                    ChatGroupId = owned.ChatGroupId,
                    UserId = _adminId
                },
                new ChatGroupMember
                {
                    ChatGroupId = owned.ChatGroupId,
                    UserId = _memberId
                },
                new ChatGroupMember
                {
                    ChatGroupId = other.ChatGroupId,
                    UserId = _memberId
                });

            db.ChatMessages.AddRange(
                new ChatMessage
                {
                    ChatGroupId = owned.ChatGroupId,
                    SenderId = _adminId,
                    Content = "In owned group"
                },
                new ChatMessage
                {
                    ChatGroupId = other.ChatGroupId,
                    SenderId = _memberId,
                    Content = "Remove me"
                },
                new ChatMessage
                {
                    ChatGroupId = other.ChatGroupId,
                    SenderId = _adminId,
                    Content = "Keep me"
                });

            db.FriendRequests.AddRange(
                new FriendRequest
                {
                    SenderId = _adminId,
                    ReceiverId = _memberId
                },
                new FriendRequest
                {
                    SenderId = _memberId,
                    ReceiverId = _adminId
                });

            db.UserSkills.Add(new UserSkill
            {
                UserId = _memberId,
                SkillId = (await db.Skills.SingleAsync()).SkillId
            });

            await db.SaveChangesAsync();
        }

        await Change("Delete", _memberId, "member@example.com");

        Assert.True(await Read(async (db, users) =>
            await users.FindByIdAsync(_memberId) == null &&
            !await db.FriendRequests.AnyAsync() &&
            !await db.UserSkills.AnyAsync() &&
            !await db.ChatGroupMembers.AnyAsync() &&
            await db.ChatGroups.CountAsync() == 1 &&
            await db.ChatMessages.CountAsync() == 1 &&
            (await db.ChatMessages.SingleAsync()).Content == "Keep me"));

        Assert.Equal(
            HttpStatusCode.Redirect,
            (await _member.GetAsync("/Profile")).StatusCode);
    }

    [Fact]
    public async Task Registration_IgnoresPostedAdminRole()
    {
        using var client = Client();

        var response = await Post(
            client,
            "/Account/Register",
            "/Account/Register",
            new()
            {
                ["DisplayName"] = "New user",
                ["Email"] = "new@example.com",
                ["Password"] = Password,
                ["ConfirmPassword"] = Password,
                ["Role"] = "Admin",
                ["IsAdmin"] = "true"
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var denied = await client.GetAsync("/Admin");

        Assert.Contains(
            "/Account/AccessDenied",
            denied.Headers.Location!.ToString());
    }

    private HttpClient Client() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    private async Task<HttpResponseMessage> Login(
        HttpClient client,
        string email,
        bool expectSuccess = true)
    {
        var response = await Post(
            client,
            "/Account/Login",
            "/Account/Login",
            new()
            {
                ["Email"] = email,
                ["Password"] = Password
            });

        if (expectSuccess)
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        return response;
    }

    private async Task Change(
        string action,
        string id,
        string? confirmation = null)
    {
        var response = await Post(
            _admin,
            "/Admin/" + action,
            "/Admin",
            new()
            {
                ["id"] = id,
                ["Confirmation"] = confirmation ?? ""
            });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> Post(
        HttpClient client,
        string endpoint,
        string tokenPage,
        Dictionary<string, string> form)
    {
        var html = await client.GetStringAsync(tokenPage);

        var input = Regex.Match(
            html,
            "<input[^>]*name=\"__RequestVerificationToken\"[^>]*>").Value;

        var value = Regex.Match(input, "value=\"([^\"]+)\"");

        Assert.True(
            value.Success,
            "The page should render an antiforgery token.");

        form["__RequestVerificationToken"] =
            WebUtility.HtmlDecode(value.Groups[1].Value);

        return await client.PostAsync(
            endpoint,
            new FormUrlEncodedContent(form));
    }

    private async Task<T> Read<T>(
        Func<ApplicationDbContext, UserManager<ApplicationUser>, Task<T>> read)
    {
        using var scope = _factory.Services.CreateScope();

        return await read(
            scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
            scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>());
    }

    private static void Check(IdentityResult result) =>
        Assert.True(
            result.Succeeded,
            string.Join("; ", result.Errors.Select(e => e.Description)));

    public void Dispose()
    {
        _admin.Dispose();
        _member.Dispose();
        _factory.Dispose();
    }

    private sealed class AdminFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection =
            new("Data Source=:memory:;Foreign Keys=True");

        public AdminFactory() => _connection.Open();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["CloudflareR2:ServiceUrl"] =
                            "https://example.invalid",

                        ["CloudflareR2:AccessKeyId"] =
                            "test-only",

                        ["CloudflareR2:SecretAccessKey"] =
                            "test-only"
                    }));

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ApplicationDbContext>();

                services.RemoveAll<
                    DbContextOptions<ApplicationDbContext>>();

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlite(_connection));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
                _connection.Dispose();
        }
    }
}