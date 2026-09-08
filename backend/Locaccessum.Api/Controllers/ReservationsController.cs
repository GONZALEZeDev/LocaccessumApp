using Locaccessum.Api.Abstractions;
using Locaccessum.Api.Authorization;
using Locaccessum.Api.Common;
using Locaccessum.Api.Contracts.Reservations;
using Locaccessum.Api.Services;
using Locaccessum.Domain.Enums;
using Locaccessum.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Locaccessum.Api.Controllers;

[ApiController]
[Route("api/inventories/{inventoryId:guid}/reservations")]
public class ReservationsController(LocaccessumDbContext db, ReservationBookingService bookingService, ICurrentUser currentUser, InventoryRoleHandler roleHandler) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = InventoryPolicies.Member)]
    public async Task<IActionResult> Calendar(Guid inventoryId, [FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to, [FromQuery] Guid? equipmentId = null)
    {
        if (from >= to)
        {
            return ApiProblem.Validation("INVALID_RANGE", "from must be before to.");
        }

        var query = db.Reservations
            .Where(r => r.Equipment.InventoryId == inventoryId
                && r.Status == ReservationStatus.Confirmed
                && r.StartsAt < to
                && r.EndsAt > from);

        if (equipmentId is not null)
        {
            query = query.Where(r => r.EquipmentId == equipmentId);
        }

        var items = await query
            .OrderBy(r => r.StartsAt)
            .Select(r => new ReservationResponse(
                r.Id,
                r.EquipmentId,
                r.Equipment.Name,
                r.Equipment.Reference,
                r.UserId,
                r.User.DisplayName,
                r.StartsAt,
                r.EndsAt,
                r.Status.ToString()))
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost]
    [Authorize(Policy = InventoryPolicies.Member)]
    public async Task<IActionResult> Create(Guid inventoryId, CreateReservationRequest req, CancellationToken ct)
    {
        var result = await bookingService.BookAsync(inventoryId, currentUser.Id, req, ct);
        switch (result)
        {
            case BookingResult.Booked booked:
                var full = await db.Reservations
                    .Include(r => r.Equipment)
                    .Include(r => r.User)
                    .SingleAsync(r => r.Id == booked.Reservation.Id, ct);
                return StatusCode(201, new ReservationResponse(full.Id, full.EquipmentId, full.Equipment.Name, full.Equipment.Reference, full.UserId, full.User.DisplayName, full.StartsAt, full.EndsAt, full.Status.ToString()));
            case BookingResult.StackFull sf:
                var pd = new ProblemDetails { Status = 409, Detail = "No free unit for the requested time range." };
                pd.Extensions["code"] = "STACK_FULL";
                pd.Extensions["unitsTotal"] = sf.UnitsTotal;
                pd.Extensions["unitsBusy"] = sf.UnitsBusy;
                return new ObjectResult(pd) { StatusCode = 409, ContentTypes = { "application/problem+json" } };
            case BookingResult.ValidationFailed vf:
                return ApiProblem.Validation(vf.Code, vf.Message);
            case BookingResult.EquipmentNotFound:
                return ApiProblem.NotFound("EQUIPMENT_NOT_FOUND", "Equipment not found.");
            default:
                throw new InvalidOperationException("Unhandled booking result.");
        }
    }

    [HttpPost("/api/reservations/{id:guid}/cancel")]
    [Authorize]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        var reservation = await db.Reservations.Include(r => r.Equipment).SingleOrDefaultAsync(r => r.Id == id, ct);
        if (reservation is null) return ApiProblem.NotFound("RESERVATION_NOT_FOUND", "Reservation not found.");

        if (reservation.Status == ReservationStatus.Cancelled)
            return ApiProblem.Conflict("ALREADY_CANCELLED", "This reservation is already cancelled.");

        var isOwnFutureBooking = reservation.UserId == currentUser.Id && reservation.StartsAt > DateTimeOffset.UtcNow;
        var allowed = isOwnFutureBooking;
        if (!allowed)
        {
            var role = await roleHandler.ResolveRoleAsync(reservation.Equipment.InventoryId, currentUser.Id);
            allowed = role is MembershipRole.Admin or MembershipRole.Owner;
        }
        if (!allowed) return ApiProblem.Forbidden("CANNOT_CANCEL", "You cannot cancel this reservation.");

        reservation.Status = ReservationStatus.Cancelled;
        reservation.CancelledAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);

        var full = await db.Reservations.Include(r => r.Equipment).Include(r => r.User).SingleAsync(r => r.Id == id, ct);
        return Ok(new ReservationResponse(full.Id, full.EquipmentId, full.Equipment.Name, full.Equipment.Reference, full.UserId, full.User.DisplayName, full.StartsAt, full.EndsAt, full.Status.ToString()));
    }
}
