using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Locaccessum.Api.Contracts.Users;
using Locaccessum.Tests.Infrastructure;

namespace Locaccessum.Tests.Api;

[Collection("db")]
public class UserSearchTests(PostgresFixture fx) : IntegrationTest(fx)
{
    [Fact]
    public async Task Search_by_exact_code_returns_summary_without_email()
    {
        var (targetUserId, targetToken) = await Register("search-target@x.io");
        var targetAuthed = await AuthedClient(targetToken);
        var meRes = await targetAuthed.GetAsync("/api/users/me");
        var me = await meRes.Content.ReadFromJsonAsync<UserResponse>();

        var (_, callerToken) = await Register("search-caller@x.io");
        var callerAuthed = await AuthedClient(callerToken);

        var res = await callerAuthed.GetAsync($"/api/users/search?code={me!.UserCode}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await res.Content.ReadAsStringAsync();
        raw.Should().NotContain("email", "UserSummaryResponse must never leak email");

        var body = await res.Content.ReadFromJsonAsync<UserSummaryResponse>();
        body!.UserId.Should().Be(targetUserId);
        body.DisplayName.Should().Be(me.DisplayName);
        body.UserCode.Should().Be(me.UserCode);
    }

    [Fact]
    public async Task Search_by_lowercased_code_is_case_insensitive()
    {
        var (targetUserId, targetToken) = await Register("search-lower@x.io");
        var targetAuthed = await AuthedClient(targetToken);
        var meRes = await targetAuthed.GetAsync("/api/users/me");
        var me = await meRes.Content.ReadFromJsonAsync<UserResponse>();

        var (_, callerToken) = await Register("search-lower-caller@x.io");
        var callerAuthed = await AuthedClient(callerToken);

        var res = await callerAuthed.GetAsync($"/api/users/search?code={me!.UserCode.ToLowerInvariant()}");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await res.Content.ReadFromJsonAsync<UserSummaryResponse>();
        body!.UserId.Should().Be(targetUserId);
    }

    [Fact]
    public async Task Search_by_unknown_code_is_404_user_not_found()
    {
        var (_, callerToken) = await Register("search-unknown-caller@x.io");
        var callerAuthed = await AuthedClient(callerToken);

        var res = await callerAuthed.GetAsync("/api/users/search?code=LOCA-000000");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var body = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("code").GetString().Should().Be("USER_NOT_FOUND");
    }

    [Fact]
    public async Task Search_without_authorization_header_is_401()
    {
        var res = await Client.GetAsync("/api/users/search?code=LOCA-000000");

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Search_without_code_query_parameter_is_400_code_required()
    {
        var (_, callerToken) = await Register("search-no-code-caller@x.io");
        var callerAuthed = await AuthedClient(callerToken);

        var res = await callerAuthed.GetAsync("/api/users/search");

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await res.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("code").GetString().Should().Be("CODE_REQUIRED");
    }
}
