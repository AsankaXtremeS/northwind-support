using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Features.Auth.Login;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public sealed class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void Validate_EmailAndPasswordGiven_IsValid()
    {
        Assert.True(_validator.Validate(new LoginRequest("sara@northwind-support.example", "anything")).IsValid);
    }

    [Theory]
    [InlineData("", "password", "Email")]
    [InlineData("sara@northwind-support.example", "", "Password")]
    public void Validate_FieldMissing_FailsOnThatField(string email, string password, string field)
    {
        var result = _validator.Validate(new LoginRequest(email, password));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == field);
    }

    [Fact]
    public void Validate_PasswordLongerThanTheLimit_Fails()
    {
        var tooLong = new string('p', LoginRequestValidator.PasswordMaxLength + 1);

        Assert.False(_validator.Validate(new LoginRequest("sara@northwind-support.example", tooLong)).IsValid);
    }
}
