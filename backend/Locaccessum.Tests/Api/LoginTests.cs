using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Locaccessum.Api.Contracts.Auth;
using Locaccessum.Tests.Infrastructure;

namespace Locaccessum.Tests.Api;

[Collection("db")]
public class LoginTests(PostgresFixture fx) : IntegrationTest(fx)
{
    [Fact]
    public async Task Login_with_correct_credentials_returns_token_that_works_on_me()
    {
        await Client.PostAsJsonAsync("/api/auth/register",
            new { email = "login-ok@x.io", password = "Passw0rd!", displayName = "Login Ok" });

        var res = await Client.PostAsJsonAsync("/api/auth/login",
            new { email = "login-ok@x.io", password = "Passw0rd!" });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<AuthResponse>();
        body!.Token.Should().NotBeNullOrEmpty();
        body.Email.Should().Be("login-ok@x.io");

        var authed = await AuthedClient(body.Token);
        var meRes = await authed.GetAsync("/api/users/me");
        meRes.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_with_wrong_password_is_401_invalid_credentials()
    {
        await Client.PostAsJsonAsync("/api/auth/register",
            new { email = "login-wrong-pw@x.io", password = "Passw0rd!", displayName = "Login Wrong" });

        var res = await Client.PostAsJsonAsync("/api/auth/login",
            new { email = "login-wrong-pw@x.io", password = "WrongPassw0rd!" });

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("code").GetString().Should().Be("INVALID_CREDENTIALS");
    }

    [Fact]
    public async Task Login_with_unknown_email_is_401_invalid_credentials()
    {
        var res = await Client.PostAsJsonAsync("/api/auth/login",
            new { email = "no-such-user@x.io", password = "Passw0rd!" });

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("code").GetString().Should().Be("INVALID_CREDENTIALS");
    }
}
