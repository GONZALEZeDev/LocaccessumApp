using FluentAssertions;
using Locaccessum.Infrastructure.Auth;

namespace Locaccessum.Tests.Auth;

public class PasswordHasherTests
{
    readonly IPasswordHasher _h = new PasswordHasher();

    [Fact] public void Hash_then_verify_true() => _h.Verify("Passw0rd!", _h.Hash("Passw0rd!")).Should().BeTrue();
    [Fact] public void Verify_wrong_password_false() => _h.Verify("nope", _h.Hash("Passw0rd!")).Should().BeFalse();
    [Fact] public void Hashes_are_salted() => _h.Hash("x").Should().NotBe(_h.Hash("x"));
}
