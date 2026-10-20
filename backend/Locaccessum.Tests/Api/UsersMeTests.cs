using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Locaccessum.Api.Contracts.Users;
using Locaccessum.Infrastructure.Auth;
using Locaccessum.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Locaccessum.Tests.Api;

[Collection("db")]
public class UsersMeTests(PostgresFixture fx) : IntegrationTest(fx)
{
    [Fact]
    public async Task Me_returns_the_authenticated_users_profile()
    {
        var (userId, token) = await Register("me-user@x.io");

        var authed = await AuthedClient(token);
        var res = await authed.GetAsync("/api/users/me");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<UserResponse>();
        body!.UserId.Should().Be(userId);
        body.Email.Should().Be("me-user@x.io");
        body.DisplayName.Should().NotBeNullOrEmpty();
        body.UserCode.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Me_without_authorization_header_is_401()
    {
        var res = await Client.GetAsync("/api/users/me");

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A validly-signed token whose subject does not match any row (a deleted account, or a forged
    /// token signed with a leaked/default secret) must not crash the endpoint. Regression test for a
    /// bug found during a security review: <c>db.Users.FindAsync</c> returning null was dereferenced
    /// with the null-forgiving operator, turning this into an unhandled 500 instead of a clean 401.
    /// </summary>
    [Fact]
    public async Task Me_with_token_for_nonexistent_user_is_401_not_500()
    {
        var tokens = Factory.Services.GetRequiredService<IJwtTokenService>();
        var ghostToken = tokens.CreateToken(Guid.NewGuid(), "ghost@x.io");

        var authed = await AuthedClient(ghostToken);
        var res = await authed.GetAsync("/api/users/me");

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
