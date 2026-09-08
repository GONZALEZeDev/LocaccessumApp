using Locaccessum.Api.Contracts.Reservations;
using Locaccessum.Domain.Entities;
using Locaccessum.Domain.Enums;
using Locaccessum.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Locaccessum.Api.Services;

/// <summary>
/// Discriminated outcome of a booking attempt. See <see cref="ReservationBookingService.BookAsync"/>.
/// </summary>
public abstract record BookingResult
{
    public sealed record Booked(Reservation Reservation) : BookingResult;

    public sealed record StackFull(int UnitsTotal, int UnitsBusy) : BookingResult;

    public sealed record ValidationFailed(string Code, string Message) : BookingResult;

    public sealed record EquipmentNotFound : BookingResult;
}

/// <summary>
/// Books the first free unit of an equipment stack (or a specific unit) for a time range, guarding
/// against double-booking with a Postgres advisory transaction lock keyed on the stack identity
/// (inventory + name + reference) plus an in-transaction overlap scan, and falling back to the
/// database's exclusion constraint (SQLSTATE 23P01) as a last-resort race guard.
/// </summary>
public class ReservationBookingService(LocaccessumDbContext db)
{
    public async Task<BookingResult> BookAsync(Guid inventoryId, Guid actingUserId, CreateReservationRequest req, CancellationToken ct)
    {
        if (req.StartsAt >= req.EndsAt)
        {
            return new BookingResult.ValidationFailed("TIME_ORDER", "startsAt must be before endsAt.");
        }

        if (req.StartsAt <= DateTimeOffset.UtcNow)
        {
            return new BookingResult.ValidationFailed("PAST_START", "startsAt must be in the future.");
        }

        if (req.EquipmentId is null && (string.IsNullOrWhiteSpace(req.Name) || string.IsNullOrWhiteSpace(req.Reference)))
        {
            return new BookingResult.ValidationFailed("MISSING_TARGET", "Provide either equipmentId or both name and reference.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        List<Equipment> candidates;
        string lockName, lockReference;

        if (req.EquipmentId is not null)
        {
            var unit = await db.Equipment.SingleOrDefaultAsync(e => e.Id == req.EquipmentId.Value && e.InventoryId == inventoryId, ct);
            if (unit is null)
            {
                await tx.RollbackAsync(ct);
                return new BookingResult.EquipmentNotFound();
            }

            if (unit.Status != EquipmentStatus.Active)
            {
                await tx.RollbackAsync(ct);
                return new BookingResult.ValidationFailed("UNIT_UNAVAILABLE", "This unit is not active.");
            }

            candidates = [unit];
            lockName = unit.Name;
            lockReference = unit.Reference;
        }
        else
        {
            candidates = await db.Equipment
                .Where(e => e.InventoryId == inventoryId && e.Name == req.Name && e.Reference == req.Reference && e.Status == EquipmentStatus.Active)
                .OrderBy(e => e.CreatedAt)
                .ToListAsync(ct);
            lockName = req.Name!;
            lockReference = req.Reference!;
        }

        if (candidates.Count == 0)
        {
            await tx.RollbackAsync(ct);
            return new BookingResult.StackFull(0, 0);
        }

        var lockKey = $"{inventoryId}|{lockName}|{lockReference}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", ct);

        Equipment? chosen = null;
        var busy = 0;
        foreach (var unit in candidates)
        {
            var clash = await db.Reservations.AnyAsync(r =>
                r.EquipmentId == unit.Id && r.Status == ReservationStatus.Confirmed &&
                r.StartsAt < req.EndsAt && r.EndsAt > req.StartsAt, ct);
            if (!clash)
            {
                chosen = unit;
                break;
            }

            busy++;
        }

        if (chosen is null)
        {
            await tx.RollbackAsync(ct);
            return new BookingResult.StackFull(candidates.Count, busy);
        }

        var reservation = new Reservation
        {
            Id = Guid.CreateVersion7(),
            EquipmentId = chosen.Id,
            UserId = actingUserId,
            StartsAt = req.StartsAt,
            EndsAt = req.EndsAt,
            Status = ReservationStatus.Confirmed,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Reservations.Add(reservation);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23P01" })
        {
            await tx.RollbackAsync(ct);
            return new BookingResult.StackFull(candidates.Count, candidates.Count);
        }

        await tx.CommitAsync(ct);
        return new BookingResult.Booked(reservation);
    }
}
