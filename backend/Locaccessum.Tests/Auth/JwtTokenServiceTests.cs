using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Locaccessum.Infrastructure.Auth;
using Microsoft.Extensions.Options;

namespace Locaccessum.Tests.Auth;

public class JwtTokenServiceTests
{
    [Fact]
    public void Token_contains_sub_and_email_and_expiry()
    {
        var opts = Options.Create(new JwtOptions { Issuer="i", Audience="a", Secret=new string('k',48), LifetimeHours=8 });
        var svc = new JwtTokenService(opts);
        var id = Guid.CreateVersion7();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(svc.CreateToken(id, "u@x.io"));
        jwt.Subject.Should().Be(id.ToString());
        jwt.Claims.Should().Contain(c => c.Type == "email" && c.Value == "u@x.io");
        jwt.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddHours(8), TimeSpan.FromMinutes(1));
    }
}
