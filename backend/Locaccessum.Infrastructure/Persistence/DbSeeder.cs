using Locaccessum.Domain.Entities;
using Locaccessum.Domain.Enums;
using Locaccessum.Infrastructure.Auth;
using Locaccessum.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;

namespace Locaccessum.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(LocaccessumDbContext db, IPasswordHasher hasher, IUserCodeGenerator codes, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct)) return;

        var now = DateTimeOffset.UtcNow;
        var alice = new User { Id = Guid.CreateVersion7(), Email = "alice@locaccessum.dev", PasswordHash = hasher.Hash("Passw0rd!"), DisplayName = "Alice", UserCode = await codes.NextAsync(ct), CreatedAt = now };
        var bob = new User { Id = Guid.CreateVersion7(), Email = "bob@locaccessum.dev", PasswordHash = hasher.Hash("Passw0rd!"), DisplayName = "Bob", UserCode = await codes.NextAsync(ct), CreatedAt = now };
        db.Users.AddRange(alice, bob);

        var inventory = new Inventory { Id = Guid.CreateVersion7(), Name = "Atelier Démo", Description = "Inventaire de démonstration", OwnerId = alice.Id, CreatedAt = now };
        db.Inventories.Add(inventory);

        db.Memberships.Add(new Membership { Id = Guid.CreateVersion7(), InventoryId = inventory.Id, UserId = alice.Id, Role = MembershipRole.Owner, JoinedAt = now });
        db.Memberships.Add(new Membership { Id = Guid.CreateVersion7(), InventoryId = inventory.Id, UserId = bob.Id, Role = MembershipRole.Member, JoinedAt = now });

        var perceuses = Enumerable.Range(0, 3)
            .Select(_ => new Equipment { Id = Guid.CreateVersion7(), InventoryId = inventory.Id, Name = "Perceuse", Reference = "BOSCH-GSB", Status = EquipmentStatus.Active, CreatedAt = now })
            .ToList();
        var projecteur = new Equipment { Id = Guid.CreateVersion7(), InventoryId = inventory.Id, Name = "Vidéoprojecteur", Reference = "EPSON-EB", Status = EquipmentStatus.Active, CreatedAt = now };
        var fourgon = new Equipment { Id = Guid.CreateVersion7(), InventoryId = inventory.Id, Name = "Fourgon", Reference = "RENAULT-MASTER", Status = EquipmentStatus.Active, CreatedAt = now };
        db.Equipment.AddRange(perceuses);
        db.Equipment.Add(projecteur);
        db.Equipment.Add(fourgon);

        db.Reservations.Add(new Reservation { Id = Guid.CreateVersion7(), EquipmentId = perceuses[0].Id, UserId = bob.Id, Status = ReservationStatus.Confirmed, StartsAt = now.AddHours(24), EndsAt = now.AddHours(26), CreatedAt = now });
        db.Reservations.Add(new Reservation { Id = Guid.CreateVersion7(), EquipmentId = projecteur.Id, UserId = bob.Id, Status = ReservationStatus.Confirmed, StartsAt = now.AddDays(3), EndsAt = now.AddDays(3).AddHours(4), CreatedAt = now });

        await db.SaveChangesAsync(ct);
    }
}
