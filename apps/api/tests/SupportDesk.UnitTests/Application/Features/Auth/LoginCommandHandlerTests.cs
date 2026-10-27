using Microsoft.Extensions.Logging.Abstractions;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Auth;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Auth.Login;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Services;

namespace SupportDesk.UnitTests.Application.Features.Auth;

public sealed class LoginCommandHandlerTests
{
    private const string SaraEmail = "sara.lindqvist@northwind-support.example";
    private const string Password = "LocalDevOnly-NotARealPassword-1";

    private static readonly DateTime ExpiresAtUtc = new(2026, 10, 27, 20, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IAgentRepository> _agents = new();
    private readonly Mock<ITokenIssuer> _tokens = new();
    private readonly Mock<IPasswordHasher> _hasherSpy;
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        // The real hasher, observed, so the tests check real hashes and can see the work done.
        var realHasher = new IdentityPasswordHasher();
        _hasherSpy = new Mock<IPasswordHasher>();
        _hasherSpy.Setup(h => h.Hash(It.IsAny<string>())).Returns<string>(realHasher.Hash);
        _hasherSpy.Setup(h => h.Verify(It.IsAny<string>(), It.IsAny<string>())).Returns<string, string>(realHasher.Verify);

        _tokens.Setup(t => t.Issue(It.IsAny<AuthUserDto>())).Returns(new IssuedToken("signed.jwt.token", ExpiresAtUtc));

        _handler = new LoginCommandHandler(_agents.Object, _hasherSpy.Object, _tokens.Object, NullLogger<LoginCommandHandler>.Instance);
    }

    private Agent GivenAgent(string email = SaraEmail, bool active = true, bool hasPassword = true)
    {
        var agent = new Agent("Sara Lindqvist", email, 6, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        if (hasPassword)
        {
            agent.SetPasswordHash(new IdentityPasswordHasher().Hash(Password));
        }

        if (!active)
        {
            agent.Deactivate();
        }

        _agents.Setup(a => a.GetByEmailAsync(email, It.IsAny<CancellationToken>())).ReturnsAsync(agent);

        return agent;
    }

    [Fact]
    public async Task HandleAsync_RightEmailAndPassword_ReturnsTheIssuedTokenAndWhoSignedIn()
    {
        GivenAgent();

        var response = await _handler.HandleAsync(new LoginRequest(SaraEmail, Password), CancellationToken.None);

        Assert.Equal("signed.jwt.token", response.Token);
        Assert.Equal(ExpiresAtUtc, response.ExpiresAtUtc);
        Assert.Equal("Sara Lindqvist", response.User.FullName);
        Assert.Equal(SaraEmail, response.User.Email);
        _tokens.Verify(t => t.Issue(response.User), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_EmailWithSurroundingSpaces_IsTrimmedBeforeTheLookup()
    {
        GivenAgent();

        var response = await _handler.HandleAsync(new LoginRequest($"  {SaraEmail} ", Password), CancellationToken.None);

        Assert.Equal(SaraEmail, response.User.Email);
    }

    [Fact]
    public async Task HandleAsync_WrongPassword_RejectsAndIssuesNoToken()
    {
        GivenAgent();

        var exception = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _handler.HandleAsync(new LoginRequest(SaraEmail, "wrong-password"), CancellationToken.None));

        Assert.Equal(InvalidCredentialsException.GenericMessage, exception.Message);
        _tokens.Verify(t => t.Issue(It.IsAny<AuthUserDto>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_UnknownEmailAndWrongPassword_FailWithTheSameMessage()
    {
        GivenAgent();

        var unknownEmail = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _handler.HandleAsync(new LoginRequest("nobody@northwind-support.example", Password), CancellationToken.None));
        var wrongPassword = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _handler.HandleAsync(new LoginRequest(SaraEmail, "wrong-password"), CancellationToken.None));

        Assert.Equal(wrongPassword.Message, unknownEmail.Message);
        _tokens.Verify(t => t.Issue(It.IsAny<AuthUserDto>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_UnknownEmail_StillDoesOnePasswordHash()
    {
        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _handler.HandleAsync(new LoginRequest("nobody@northwind-support.example", Password), CancellationToken.None));

        // The same PBKDF2 cost as a real check, so response time does not reveal unknown emails.
        _hasherSpy.Verify(h => h.Hash(Password), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_InactiveAgentWithTheRightPassword_RejectsWithTheSameMessage()
    {
        GivenAgent(active: false);

        var exception = await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _handler.HandleAsync(new LoginRequest(SaraEmail, Password), CancellationToken.None));

        Assert.Equal(InvalidCredentialsException.GenericMessage, exception.Message);
        _tokens.Verify(t => t.Issue(It.IsAny<AuthUserDto>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_AgentWithNoPasswordSet_Rejects()
    {
        GivenAgent(hasPassword: false);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _handler.HandleAsync(new LoginRequest(SaraEmail, Password), CancellationToken.None));

        _tokens.Verify(t => t.Issue(It.IsAny<AuthUserDto>()), Times.Never);
    }
}
