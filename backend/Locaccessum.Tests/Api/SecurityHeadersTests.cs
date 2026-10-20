using FluentAssertions;
using Locaccessum.Tests.Infrastructure;

namespace Locaccessum.Tests.Api;

/// <summary>
/// Baseline hardening headers found missing during a security review (neither the API nor the
/// frontend sent any). These must be present on every response, including a plain unauthenticated
/// one, so <c>/health</c> is used as a stand-in for "any response".
/// </summary>
[Collection("db")]
public class SecurityHeadersTests(PostgresFixture fx) : IAsyncLifetime
{
    ApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory(fx);
        await _factory.InitializeAsync();
    }

    public Task DisposeAsync() => _factory.DisposeAsync().AsTask();

    [Fact]
    public async Task Responses_carry_baseline_security_headers()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/health");

        res.Headers.TryGetValues("X-Content-Type-Options", out var nosniff).Should().BeTrue();
        nosniff!.Should().ContainSingle("nosniff");

        res.Headers.TryGetValues("X-Frame-Options", out var frameOptions).Should().BeTrue();
        frameOptions!.Should().ContainSingle("DENY");

        res.Headers.TryGetValues("Referrer-Policy", out var referrer).Should().BeTrue();
        referrer!.Should().ContainSingle("no-referrer");

        res.Headers.TryGetValues("Content-Security-Policy", out var csp).Should().BeTrue();
        csp!.Should().ContainSingle(v => v.Contains("default-src 'none'"));

        res.Headers.TryGetValues("Strict-Transport-Security", out var hsts).Should().BeTrue();
        hsts!.Should().ContainSingle(v => v.Contains("max-age="));
    }
}
