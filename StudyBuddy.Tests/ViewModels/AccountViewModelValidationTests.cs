using System.ComponentModel.DataAnnotations;
using StudyBuddy.ViewModels.Account;
using Xunit;

namespace StudyBuddy.Tests.ViewModels;

// Controller unit tests don't run [Required], [EmailAddress], etc. (ModelState is
// always valid unless a test sets an error), so the validation rules are tested here.
public class AccountViewModelValidationTests
{
    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    private static RegisterViewModel ValidRegister() => new()
    {
        DisplayName = "Sam Student",
        Email = "sam@example.com",
        Password = "Password123!",
        ConfirmPassword = "Password123!"
    };

    [Fact]
    public void Register_ValidModel_HasNoErrors()
    {
        Assert.Empty(Validate(ValidRegister()));
    }

    [Fact]
    public void Register_MissingDisplayName_Fails()
    {
        var model = ValidRegister();
        model.DisplayName = "";

        Assert.Contains(Validate(model), r => r.MemberNames.Contains(nameof(RegisterViewModel.DisplayName)));
    }

    [Fact]
    public void Register_DisplayNameOver50Chars_Fails()
    {
        var model = ValidRegister();
        model.DisplayName = new string('a', 51);

        Assert.Contains(Validate(model), r => r.ErrorMessage == "Display name cannot exceed 50 characters.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Register_InvalidEmail_Fails(string email)
    {
        var model = ValidRegister();
        model.Email = email;

        Assert.Contains(Validate(model), r => r.MemberNames.Contains(nameof(RegisterViewModel.Email)));
    }

    [Fact]
    public void Register_PasswordShorterThan8_Fails()
    {
        var model = ValidRegister();
        model.Password = model.ConfirmPassword = "short1!";

        Assert.Contains(Validate(model), r => r.ErrorMessage == "Password must be at least 8 characters long.");
    }

    [Fact]
    public void Register_PasswordsDoNotMatch_Fails()
    {
        var model = ValidRegister();
        model.ConfirmPassword = "Different123!";

        Assert.Contains(Validate(model), r => r.ErrorMessage == "The passwords do not match.");
    }

    [Fact]
    public void Login_ValidModel_HasNoErrors()
    {
        Assert.Empty(Validate(new LoginViewModel { Email = "sam@example.com", Password = "pw" }));
    }

    [Fact]
    public void Login_MissingFields_Fails()
    {
        var results = Validate(new LoginViewModel());

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(LoginViewModel.Email)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(LoginViewModel.Password)));
    }
}
