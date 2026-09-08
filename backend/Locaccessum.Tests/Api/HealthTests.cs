using System.Net;
using FluentAssertions;
using Locaccessum.Tests.Infrastructure;

namespace Locaccessum.Tests.Api;

[Collection("db")]
public class HealthTests(PostgresFixture fx) : IAsyncLifetime
{
    ApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory(fx);
        await _factory.InitializeAsync();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task Health_returns_ok()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/health");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        (await res.Content.ReadAsStringAsync()).Should().Contain("ok");
    }
}
