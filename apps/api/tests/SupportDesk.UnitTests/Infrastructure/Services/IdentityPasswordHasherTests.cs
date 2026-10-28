using SupportDesk.Infrastructure.Services;

namespace SupportDesk.UnitTests.Infrastructure.Services;

public sealed class IdentityPasswordHasherTests
{
    private const string Password = "unit-tests-only-password";

    private readonly IdentityPasswordHasher _hasher = new();

    [Fact]
    public void Verify_RightPassword_ReturnsTrue()
    {
        var hash = _hasher.Hash(Password);

        Assert.True(_hasher.Verify(hash, Password));
    }

    [Theory]
    [InlineData("unit-tests-only-password-2")]
    [InlineData("UNIT-TESTS-ONLY-PASSWORD")]
    [InlineData("")]
    public void Verify_WrongPassword_ReturnsFalse(string attempt)
    {
        var hash = _hasher.Hash(Password);

        Assert.False(_hasher.Verify(hash, attempt));
    }

    [Fact]
    public void Hash_SamePasswordTwice_GivesDifferentHashesThatBothVerify()
    {
        var first = _hasher.Hash(Password);
        var second = _hasher.Hash(Password);

        Assert.NotEqual(first, second);
        Assert.True(_hasher.Verify(first, Password));
        Assert.True(_hasher.Verify(second, Password));
    }

    [Fact]
    public void Hash_AnyPassword_DoesNotContainThePassword()
    {
        var hash = _hasher.Hash(Password);

        Assert.DoesNotContain(Password, hash, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("not base64 at all!")]
    [InlineData("")]
    [InlineData("AAAA")]
    public void Verify_MalformedHash_ReturnsFalse(string hash)
    {
        Assert.False(_hasher.Verify(hash, Password));
    }
}
