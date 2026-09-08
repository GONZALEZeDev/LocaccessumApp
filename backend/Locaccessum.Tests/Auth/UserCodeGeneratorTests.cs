using System.Text.RegularExpressions;
using FluentAssertions;
using Locaccessum.Domain.Entities;
using Locaccessum.Infrastructure.Identity;
using Locaccessum.Tests.Infrastructure;

namespace Locaccessum.Tests.Auth;

[Collection("db")]
public partial class UserCodeGeneratorTests(PostgresFixture fx)
{
    [Fact]
    public async Task Generates_200_unique_codes_matching_the_expected_format()
    {
        await using var db = fx.NewDbContext();
        var sut = new UserCodeGenerator(db);

        var codes = new List<string>();
        for (var i = 0; i < 200; i++)
        {
            var code = await sut.NextAsync();

            db.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                Email = $"user-{i}@example.com",
                PasswordHash = "irrelevant-hash",
                DisplayName = $"User {i}",
                UserCode = code,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();

            codes.Add(code);
        }

        codes.Should().OnlyContain(c => CodePattern().IsMatch(c));
        codes.Should().OnlyHaveUniqueItems();
    }

    [GeneratedRegex("^LOCA-[0-9A-HJ-NP-Z]{6}$")]
    private static partial Regex CodePattern();
}
