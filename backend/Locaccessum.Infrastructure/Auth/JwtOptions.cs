namespace Locaccessum.Infrastructure.Auth;

public class JwtOptions
{
    public string Issuer { get; set; } = "";

    public string Audience { get; set; } = "";

    public string Secret { get; set; } = "";

    public int LifetimeHours { get; set; } = 8;
}
