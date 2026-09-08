using Locaccessum.Api.Authorization;
using Locaccessum.Api.Common;
using Locaccessum.Api.Contracts.Members;
using Locaccessum.Domain.Enums;
using Locaccessum.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Locaccessum.Api.Controllers;

[ApiController]
[Route("api/inventories/{inventoryId:guid}/members")]
public class MembersController(LocaccessumDbContext db) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = InventoryPolicies.Member)]
    public async Task<IActionResult> List(Guid inventoryId)
    {
        var members = await db.Memberships
            .Where(m => m.InventoryId == inventoryId)
            .Include(m => m.User)
            .Select(m => new MemberResponse(m.UserId, m.User.DisplayName, m.User.UserCode, m.Role.ToString(), m.JoinedAt))
            .ToListAsync();

        return Ok(members);
    }

    [HttpPatch("{userId:guid}")]
    [Authorize(Policy = InventoryPolicies.Admin)]
    public async Task<IActionResult> UpdateRole(Guid inventoryId, Guid userId, UpdateMemberRoleRequest req)
    {
        if (!Enum.TryParse<MembershipRole>(req.Role, ignoreCase: true, out var newRole) || !Enum.IsDefined(newRole) || newRole == MembershipRole.Owner)
        {
            return ApiProblem.Validation("INVALID_ROLE", "Role must be Admin or Member.");
        }

        var target = await db.Memberships.SingleOrDefaultAsync(m => m.InventoryId == inventoryId && m.UserId == userId);
        if (target is null)
        {
            return ApiProblem.NotFound("MEMBER_NOT_FOUND", "Member not found.");
        }

        if (target.Role == MembershipRole.Owner)
        {
            return ApiProblem.Validation("CANNOT_MODIFY_OWNER", "The owner's role cannot be changed.");
        }

        target.Role = newRole;
        await db.SaveChangesAsync();

        return Ok();
    }

    [HttpDelete("{userId:guid}")]
    [Authorize(Policy = InventoryPolicies.Admin)]
    public async Task<IActionResult> Remove(Guid inventoryId, Guid userId)
    {
        var target = await db.Memberships.SingleOrDefaultAsync(m => m.InventoryId == inventoryId && m.UserId == userId);
        if (target is null)
        {
            return ApiProblem.NotFound("MEMBER_NOT_FOUND", "Member not found.");
        }

        if (target.Role == MembershipRole.Owner)
        {
            return ApiProblem.Validation("CANNOT_REMOVE_OWNER", "The owner cannot be removed.");
        }

        db.Memberships.Remove(target);

        var now = DateTimeOffset.UtcNow;
        var upcoming = await db.Reservations
            .Where(r => r.Equipment.InventoryId == inventoryId && r.UserId == userId && r.Status == ReservationStatus.Confirmed && r.EndsAt > now)
            .ToListAsync();

        foreach (var r in upcoming)
        {
            r.Status = ReservationStatus.Cancelled;
            r.CancelledAt = now;
        }

        await db.SaveChangesAsync();

        return NoContent();
    }
}
