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

    /// <summary>Same cap as registration (see RegisterTests) — login's hasher.Verify call does the
    /// same PBKDF2 work per request, so it needs the same bound.</summary>
    [Fact]
    public async Task Login_with_overlong_password_is_400()
    {
        var res = await Client.PostAsJsonAsync("/api/auth/login",
            new { email = "someone@x.io", password = new string('a', 129) });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Security review finding: unlimited login attempts allowed a password brute-force with no
    /// friction. The "auth" rate-limit policy caps attempts per client; the 11th rapid call in the
    /// same window must be rejected with 429 rather than evaluated as another credential check.
    /// </summary>
    [Fact]
    public async Task Login_is_rate_limited_after_too_many_attempts()
    {
        HttpResponseMessage? last = null;
        for (var i = 0; i < 11; i++)
        {
            last = await Client.PostAsJsonAsync("/api/auth/login",
                new { email = "rate-limit-probe@x.io", password = "wrong-password" });
        }

        last!.StatusCode.Should().Be((HttpStatusCode)429);
    }
}
