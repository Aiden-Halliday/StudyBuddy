using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using StudyBuddy.Controllers;
using StudyBuddy.Models;
using StudyBuddy.ViewModels.Account;
using Xunit;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;
namespace StudyBuddy.Tests.Controllers;

public class AccountControllerTests
{
    private readonly Mock<UserManager<ApplicationUser>> _userManager;
    private readonly Mock<SignInManager<ApplicationUser>> _signInManager;
    private readonly AccountController _controller;

    public AccountControllerTests()
    {
        _userManager = CreateUserManagerMock();
        _signInManager = CreateSignInManagerMock(_userManager.Object);

        _controller = new AccountController(_userManager.Object, _signInManager.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    // ---------------------------------------------------------------
    // Register (GET)
    // ---------------------------------------------------------------

    [Fact]
    public void Register_Get_ReturnsView()
    {
        var result = _controller.Register();

        Assert.IsType<ViewResult>(result);
    }

    // ---------------------------------------------------------------
    // Register (POST)
    // ---------------------------------------------------------------

    [Fact]
    public async Task Register_Post_InvalidModelState_ReturnsViewWithModel_AndDoesNotCreateUser()
    {
        var model = ValidRegisterModel();
        _controller.ModelState.AddModelError("Email", "Required");

        var result = await _controller.Register(model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        _userManager.Verify(
            m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task Register_Post_Success_CreatesUserWithModelValues()
    {
        var model = ValidRegisterModel();
        ApplicationUser? createdUser = null;
        string? passwordUsed = null;

        _userManager
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .Callback<ApplicationUser, string>((u, p) => { createdUser = u; passwordUsed = p; })
            .ReturnsAsync(IdentityResult.Success);

        var before = DateTime.UtcNow;
        await _controller.Register(model);
        var after = DateTime.UtcNow;

        Assert.NotNull(createdUser);
        Assert.Equal(model.Email, createdUser!.UserName);
        Assert.Equal(model.Email, createdUser.Email);
        Assert.Equal(model.DisplayName, createdUser.DisplayName);
        Assert.InRange(createdUser.CreatedUtc, before, after);
        Assert.Equal(model.Password, passwordUsed);
    }

    [Fact]
    public async Task Register_Post_Success_SignsInAndRedirectsToHome()
    {
        var model = ValidRegisterModel();

        _userManager
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _controller.Register(model);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);

        _signInManager.Verify(
            s => s.SignInAsync(
                It.Is<ApplicationUser>(u => u.Email == model.Email),
                false,
                It.IsAny<string?>()),
            Times.Once);
    }

    [Fact]
    public async Task Register_Post_Failure_AddsIdentityErrorsToModelState_AndReturnsView()
    {
        var model = ValidRegisterModel();

        _userManager
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(
                new IdentityError { Code = "DuplicateEmail", Description = "Email is already taken." },
                new IdentityError { Code = "PasswordTooWeak", Description = "Password needs a digit." }));

        var result = await _controller.Register(model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.False(_controller.ModelState.IsValid);

        var errors = _controller.ModelState[string.Empty]!.Errors.Select(e => e.ErrorMessage).ToList();
        Assert.Contains("Email is already taken.", errors);
        Assert.Contains("Password needs a digit.", errors);
    }

    [Fact]
    public async Task Register_Post_Failure_DoesNotSignIn()
    {
        _userManager
            .Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Nope" }));

        await _controller.Register(ValidRegisterModel());

        _signInManager.Verify(
            s => s.SignInAsync(It.IsAny<ApplicationUser>(), It.IsAny<bool>(), It.IsAny<string?>()),
            Times.Never);
    }

    // ---------------------------------------------------------------
    // Login (GET)
    // ---------------------------------------------------------------

    [Fact]
    public void Login_Get_ReturnsViewWithEmptyLoginViewModel()
    {
        var result = _controller.Login();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<LoginViewModel>(view.Model);
        Assert.Equal("", model.Email);
        Assert.Equal("", model.Password);
    }

    // ---------------------------------------------------------------
    // Login (POST)
    // ---------------------------------------------------------------

    [Fact]
    public async Task Login_Post_InvalidModelState_ReturnsViewWithModel_AndDoesNotLookUpUser()
    {
        var model = new LoginViewModel { Email = "", Password = "" };
        _controller.ModelState.AddModelError("Email", "Required");

        var result = await _controller.Login(model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        _userManager.Verify(m => m.FindByEmailAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Login_Post_ValidCredentials_RedirectsToHome()
    {
        var user = new ApplicationUser { Email = "sam@example.com", UserName = "sam@example.com" };
        _userManager.Setup(m => m.FindByEmailAsync("sam@example.com")).ReturnsAsync(user);
        SetupPasswordSignIn(user, "Password123!", SignInResult.Success);

        var result = await _controller.Login(new LoginViewModel
        {
            Email = "sam@example.com",
            Password = "Password123!"
        });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Home", redirect.ControllerName);
    }

    [Fact]
    public async Task Login_Post_TrimsEmailBeforeLookup()
    {
        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        await _controller.Login(new LoginViewModel
        {
            Email = "   sam@example.com  ",
            Password = "whatever"
        });

        _userManager.Verify(m => m.FindByEmailAsync("sam@example.com"), Times.Once);
    }

    [Fact]
    public async Task Login_Post_UsesLockoutOnFailure_AndNotPersistent()
    {
        var user = new ApplicationUser { Email = "sam@example.com" };
        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);
        SetupPasswordSignIn(user, "pw", SignInResult.Success);

        await _controller.Login(new LoginViewModel { Email = "sam@example.com", Password = "pw" });

        _signInManager.Verify(
            s => s.PasswordSignInAsync(user, "pw", false, true),
            Times.Once);
    }

    [Fact]
    public async Task Login_Post_UnknownEmail_ReturnsViewWithGenericError_AndDoesNotAttemptSignIn()
    {
        var model = new LoginViewModel { Email = "nobody@example.com", Password = "pw" };
        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);

        var result = await _controller.Login(model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        AssertHasGenericLoginError();

        _signInManager.Verify(
            s => s.PasswordSignInAsync(
                It.IsAny<ApplicationUser>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()),
            Times.Never);
    }

    [Fact]
    public async Task Login_Post_WrongPassword_ReturnsViewWithGenericError()
    {
        var user = new ApplicationUser { Email = "sam@example.com" };
        var model = new LoginViewModel { Email = "sam@example.com", Password = "wrong" };
        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);
        SetupPasswordSignIn(user, "wrong", SignInResult.Failed);

        var result = await _controller.Login(model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        AssertHasGenericLoginError();
    }

    [Fact]
    public async Task Login_Post_LockedOutUser_ReturnsViewWithGenericError()
    {
        var user = new ApplicationUser { Email = "sam@example.com" };
        _userManager.Setup(m => m.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync(user);
        SetupPasswordSignIn(user, "pw", SignInResult.LockedOut);

        var result = await _controller.Login(new LoginViewModel { Email = "sam@example.com", Password = "pw" });

        Assert.IsType<ViewResult>(result);
        AssertHasGenericLoginError();
    }

    [Fact]
    public async Task Login_Post_UnknownEmailAndWrongPassword_ShowSameMessage()
    {
        // Guards against user enumeration: both failures must look identical to the user.
        _userManager.Setup(m => m.FindByEmailAsync("nobody@example.com"))
            .ReturnsAsync((ApplicationUser?)null);
        await _controller.Login(new LoginViewModel { Email = "nobody@example.com", Password = "pw" });
        var unknownEmailError = _controller.ModelState[string.Empty]!.Errors.Single().ErrorMessage;

        var other = new AccountController(_userManager.Object, _signInManager.Object);
        var user = new ApplicationUser { Email = "sam@example.com" };
        _userManager.Setup(m => m.FindByEmailAsync("sam@example.com")).ReturnsAsync(user);
        SetupPasswordSignIn(user, "wrong", SignInResult.Failed);
        await other.Login(new LoginViewModel { Email = "sam@example.com", Password = "wrong" });
        var wrongPasswordError = other.ModelState[string.Empty]!.Errors.Single().ErrorMessage;

        Assert.Equal(unknownEmailError, wrongPasswordError);
    }

    // ---------------------------------------------------------------
    // Logout
    // ---------------------------------------------------------------

    [Fact]
    public async Task Logout_SignsOutAndRedirectsToLogin()
    {
        var result = await _controller.Logout();

        _signInManager.Verify(s => s.SignOutAsync(), Times.Once);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AccountController.Login), redirect.ActionName);
        Assert.Null(redirect.ControllerName);
    }

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private static RegisterViewModel ValidRegisterModel() => new()
    {
        DisplayName = "Sam Student",
        Email = "sam@example.com",
        Password = "Password123!",
        ConfirmPassword = "Password123!"
    };

    private void SetupPasswordSignIn(ApplicationUser user, string password, SignInResult result)
    {
        _signInManager
            .Setup(s => s.PasswordSignInAsync(user, password, It.IsAny<bool>(), It.IsAny<bool>()))
            .ReturnsAsync(result);
    }

    private void AssertHasGenericLoginError()
    {
        Assert.False(_controller.ModelState.IsValid);
        var errors = _controller.ModelState[string.Empty]!.Errors;
        Assert.Contains(errors, e => e.ErrorMessage == "Invalid email or password.");
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock()
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        return new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static Mock<SignInManager<ApplicationUser>> CreateSignInManagerMock(
        UserManager<ApplicationUser> userManager)
    {
        return new Mock<SignInManager<ApplicationUser>>(
            userManager,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            Mock.Of<IOptions<IdentityOptions>>(),
            Mock.Of<ILogger<SignInManager<ApplicationUser>>>(),
            Mock.Of<IAuthenticationSchemeProvider>(),
            Mock.Of<IUserConfirmation<ApplicationUser>>());
    }
}
