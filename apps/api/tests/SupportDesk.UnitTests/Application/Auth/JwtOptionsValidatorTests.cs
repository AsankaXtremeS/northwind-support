using SupportDesk.Application.Auth;

namespace SupportDesk.UnitTests.Application.Auth;

public class JwtOptionsValidatorTests
{
    private readonly JwtOptionsValidator _validator = new();

    private static JwtOptions ValidOptions() => new()
    {
        Issuer = "SupportDesk.Api",
        Audience = "SupportDesk.Web",
        SigningKey = new string('k', JwtOptions.MinimumSigningKeyBytes),
        LifetimeMinutes = 480
    };

    [Fact]
    public void Validate_WithValidSettings_Succeeds()
    {
        Assert.True(_validator.Validate(null, ValidOptions()).Succeeded);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithNoSigningKey_FailsPointingToWhereToSetIt(string key)
    {
        var options = ValidOptions();
        options.SigningKey = key;

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("Jwt__SigningKey"));
    }

    [Fact]
    public void Validate_WithAKeyOneByteTooShort_Fails()
    {
        var options = ValidOptions();
        options.SigningKey = new string('k', JwtOptions.MinimumSigningKeyBytes - 1);

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("at least 32 bytes"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(JwtOptionsValidator.MaximumLifetimeMinutes + 1)]
    public void Validate_WithALifetimeOutOfRange_Fails(int minutes)
    {
        var options = ValidOptions();
        options.LifetimeMinutes = minutes;

        Assert.True(_validator.Validate(null, options).Failed);
    }

    [Fact]
    public void Validate_WithNoIssuerOrAudience_FailsForBoth()
    {
        var options = ValidOptions();
        options.Issuer = "";
        options.Audience = "";

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures!, failure => failure.Contains("Issuer"));
        Assert.Contains(result.Failures!, failure => failure.Contains("Audience"));
    }
}
