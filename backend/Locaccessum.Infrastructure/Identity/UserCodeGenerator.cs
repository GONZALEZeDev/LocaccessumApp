using System.Security.Cryptography;
using Locaccessum.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Locaccessum.Infrastructure.Identity;

public class UserCodeGenerator(LocaccessumDbContext db) : IUserCodeGenerator
{
    const string Alphabet = "0123456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    const int CodeLength = 6;
    const int MaxAttempts = 10;

    public async Task<string> NextAsync(CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var candidate = $"LOCA-{new string(RandomNumberGenerator.GetItems<char>(Alphabet, CodeLength))}";

            var exists = await db.Users.AnyAsync(u => u.UserCode == candidate, ct);
            if (!exists)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Could not generate a unique user code after 10 attempts.");
    }
}
