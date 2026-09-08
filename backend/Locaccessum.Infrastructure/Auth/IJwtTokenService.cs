namespace Locaccessum.Infrastructure.Auth;

public interface IJwtTokenService
{
    string CreateToken(Guid userId, string email);
}
