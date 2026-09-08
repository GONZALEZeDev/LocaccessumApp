using FluentAssertions;
using Locaccessum.Infrastructure.Auth;
using Locaccessum.Infrastructure.Identity;
using Locaccessum.Infrastructure.Persistence;
using Locaccessum.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Locaccessum.Tests.Persistence;

[Collection("db")]
public class SeederTests(PostgresFixture fx) : IntegrationTest(fx)
{
    [Fact]
    public async Task SeedAsync_is_idempotent_and_creates_perceuse_stack_of_three()
    {
        await using var db = Fixture.NewDbContext();
        var hasher = new PasswordHasher();
        var codes = new UserCodeGenerator(db);

        await DbSeeder.SeedAsync(db, hasher, codes);
        var countAfterFirst = await db.Users.CountAsync();
        countAfterFirst.Should().Be(2);

        await DbSeeder.SeedAsync(db, hasher, codes);
        var countAfterSecond = await db.Users.CountAsync();
        countAfterSecond.Should().Be(countAfterFirst);

        var perceuseCount = await db.Equipment.CountAsync(e => e.Name == "Perceuse" && e.Reference == "BOSCH-GSB");
        perceuseCount.Should().Be(3);
    }
}
