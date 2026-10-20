using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Locaccessum.Api.Contracts.Auth;
using Locaccessum.Tests.Infrastructure;

namespace Locaccessum.Tests.Api;

[Collection("db")]
public class RegisterTests(PostgresFixture fx) : IntegrationTest(fx)
{
    [Fact]
    public async Task Register_creates_user_and_returns_token_and_code()
    {
        var res = await Client.PostAsJsonAsync("/api/auth/register",
            new { email = "a@x.io", password = "Passw0rd!", displayName = "Alice" });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<AuthResponse>();
        body!.Token.Should().NotBeNullOrEmpty();
        body.UserCode.Should().MatchRegex("^LOCA-[0-9A-HJ-NP-Z]{6}$");
    }

    [Fact]
    public async Task Duplicate_email_is_409()
    {
        await Client.PostAsJsonAsync("/api/auth/register", new { email = "dup@x.io", password = "Passw0rd!", displayName = "D" });
        var res = await Client.PostAsJsonAsync("/api/auth/register", new { email = "dup@x.io", password = "Passw0rd!", displayName = "D2" });
        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("bad-email", "Passw0rd!")]
    [InlineData("ok@x.io", "short")]
    public async Task Invalid_payload_is_400(string email, string password)
    {
        var res = await Client.PostAsJsonAsync("/api/auth/register", new { email, password, displayName = "X" });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>Shares the "auth" rate-limit policy with login (see LoginTests) so a single client can't
    /// sidestep the login throttle by hammering registration instead.</summary>
    [Fact]
    public async Task Register_is_rate_limited_after_too_many_attempts()
    {
        HttpResponseMessage? last = null;
        for (var i = 0; i < 11; i++)
        {
            last = await Client.PostAsJsonAsync("/api/auth/register",
                new { email = $"rate-limit-probe-{i}@x.io", password = "Passw0rd!", displayName = "RL" });
        }

        last!.StatusCode.Should().Be((HttpStatusCode)429);
    }
}
