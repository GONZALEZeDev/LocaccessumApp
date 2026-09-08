using Microsoft.AspNetCore.Identity;

namespace Locaccessum.Infrastructure.Auth;

public class PasswordHasher : IPasswordHasher
{
    readonly Microsoft.AspNetCore.Identity.PasswordHasher<object> _inner = new();

    public string Hash(string password) => _inner.HashPassword(new object(), password);

    public bool Verify(string password, string hash)
    {
        var result = _inner.VerifyHashedPassword(new object(), hash, password);
        return result is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
    }
}
