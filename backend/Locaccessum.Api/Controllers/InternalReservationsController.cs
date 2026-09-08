using Locaccessum.Api.Common;
using Locaccessum.Api.Contracts.Reservations;
using Locaccessum.Domain.Enums;
using Locaccessum.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Locaccessum.Api.Controllers;

[ApiController]
[Route("api/internal/reservations")]
public class InternalReservationsController(LocaccessumDbContext db) : ControllerBase
{
    [HttpGet("upcoming")]
    [Authorize(AuthenticationSchemes = "Internal", Policy = "InternalKey")]
    public async Task<IActionResult> Upcoming([FromQuery] int windowHours = 24)
    {
        var clamped = Math.Clamp(windowHours, 1, 168);
        var now = DateTimeOffset.UtcNow;
        var from = now.AddHours(clamped - 1);
        var to = now.AddHours(clamped + 1);

        var items = await db.Reservations
            .Where(r => r.Status == ReservationStatus.Confirmed
                && r.ReminderSentAt == null
                && r.StartsAt >= from
                && r.StartsAt < to)
            .Select(r => new UpcomingReservationResponse(
                r.Id,
                r.User.Email,
                r.User.DisplayName,
                r.Equipment.Name,
                r.Equipment.Reference,
                r.Equipment.Inventory.Name,
                r.StartsAt,
                r.EndsAt))
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost("{id:guid}/reminder-sent")]
    [Authorize(AuthenticationSchemes = "Internal", Policy = "InternalKey")]
    public async Task<IActionResult> MarkReminderSent(Guid id)
    {
        var reservation = await db.Reservations.SingleOrDefaultAsync(r => r.Id == id);
        if (reservation is null) return ApiProblem.NotFound("RESERVATION_NOT_FOUND", "Reservation not found.");

        if (reservation.ReminderSentAt is null)
        {
            reservation.ReminderSentAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        return NoContent();
    }
}
