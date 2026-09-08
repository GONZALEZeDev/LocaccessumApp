using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Locaccessum.Api.Authorization;

public class InternalApiKeyAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Internal-Api-Key", out var provided) || provided.Count == 0)
            return Task.FromResult(AuthenticateResult.Fail("Missing API key."));

        var expected = configuration["InternalApiKey"];
        if (string.IsNullOrEmpty(expected))
            return Task.FromResult(AuthenticateResult.Fail("Server misconfigured."));

        var providedBytes = System.Text.Encoding.UTF8.GetBytes(provided.ToString());
        var expectedBytes = System.Text.Encoding.UTF8.GetBytes(expected);
        if (providedBytes.Length != expectedBytes.Length || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes))
            return Task.FromResult(AuthenticateResult.Fail("Invalid API key."));

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Role, "internal")], Scheme.Name);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
