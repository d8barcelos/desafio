using FluentAssertions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using OrderService.Application.Auth;
using OrderService.Application.Auth.Dtos;
using OrderService.Application.Common.Abstractions;
using OrderService.Domain.Entities;

namespace OrderService.Application.Tests.Auth;

public class AuthServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenGenerator _tokens = Substitute.For<IJwtTokenGenerator>();

    private AuthService BuildSut() => new(_users, _hasher, _tokens);

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsToken()
    {
        var user = new User(Guid.NewGuid(), "alice", "hash", Roles.Customer, Guid.NewGuid());
        _users.GetByUsernameAsync("alice", Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("secret", "hash").Returns(true);
        var expires = DateTimeOffset.UtcNow.AddHours(1);
        _tokens.Generate(user).Returns(new JwtToken("token-value", expires));

        var result = await BuildSut().LoginAsync(new LoginRequest("alice", "secret"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Token.Should().Be("token-value");
        result.Value.ExpiresAt.Should().Be(expires);
    }

    [Fact]
    public async Task LoginAsync_UnknownUser_ReturnsInvalidCredentials()
    {
        _users.GetByUsernameAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).ReturnsNull();

        var result = await BuildSut().LoginAsync(new LoginRequest("ghost", "secret"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("invalid_credentials");
        _tokens.DidNotReceiveWithAnyArgs().Generate(default!);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsInvalidCredentials()
    {
        var user = new User(Guid.NewGuid(), "alice", "hash", Roles.Customer, Guid.NewGuid());
        _users.GetByUsernameAsync("alice", Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("bad", "hash").Returns(false);

        var result = await BuildSut().LoginAsync(new LoginRequest("alice", "bad"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("invalid_credentials");
    }

    [Theory]
    [InlineData("", "secret")]
    [InlineData("alice", "")]
    [InlineData(" ", "secret")]
    public async Task LoginAsync_MissingFields_ReturnsInvalidCredentials(string username, string password)
    {
        var result = await BuildSut().LoginAsync(new LoginRequest(username, password), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("invalid_credentials");
        await _users.DidNotReceiveWithAnyArgs().GetByUsernameAsync(default!, default);
    }
}
