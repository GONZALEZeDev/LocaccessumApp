using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Locaccessum.Api.Contracts.Users;
using Locaccessum.Tests.Infrastructure;

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
}
