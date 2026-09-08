using Locaccessum.Api.Abstractions;
using Locaccessum.Api.Authorization;
using Locaccessum.Api.Common;
using Locaccessum.Api.Contracts.Invitations;
using Locaccessum.Domain.Entities;
using Locaccessum.Domain.Enums;
using Locaccessum.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Locaccessum.Api.Controllers;

[ApiController]
[Route("api/inventories/{inventoryId:guid}/invitations")]
public class InvitationsController(LocaccessumDbContext db, ICurrentUser currentUser, InventoryRoleHandler roleHandler) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = InventoryPolicies.Admin)]
    public async Task<IActionResult> Create(Guid inventoryId, CreateInvitationRequest req)
    {
        if (!Enum.TryParse<MembershipRole>(req.Role, ignoreCase: true, out var role) || !Enum.IsDefined(role) || role == MembershipRole.Owner)
        {
            return ApiProblem.Validation("INVALID_ROLE", "Role must be Admin or Member.");
        }

        var normalized = req.UserCode.Trim().ToUpperInvariant();
        var targetUser = await db.Users.SingleOrDefaultAsync(u => u.UserCode.ToUpper() == normalized);
        if (targetUser is null)
        {
            return ApiProblem.NotFound("USER_NOT_FOUND", "No user with that code.");
        }

        if (await db.Memberships.AnyAsync(m => m.InventoryId == inventoryId && m.UserId == targetUser.Id))
        {
            return ApiProblem.Conflict("ALREADY_MEMBER", "This user is already a member.");
        }

        var inventory = await db.Inventories.FindAsync(inventoryId);
        if (inventory is null)
        {
            return ApiProblem.NotFound("INVENTORY_NOT_FOUND", "Inventory not found.");
        }

        var invitation = new Invitation
        {
            Id = Guid.CreateVersion7(),
            InventoryId = inventoryId,
            InvitedUserId = targetUser.Id,
            InvitedByUserId = currentUser.Id,
            Role = role,
            Status = InvitationStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Invitations.Add(invitation);

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            return ApiProblem.Conflict("INVITATION_PENDING", "An invitation is already pending for this user.");
        }

        var response = new InvitationResponse(
            invitation.Id,
            invitation.InventoryId,
            inventory.Name,
            invitation.InvitedUserId,
            invitation.InvitedByUserId,
            invitation.Role.ToString(),
            invitation.Status.ToString(),
            invitation.CreatedAt);

        return Created($"/api/inventories/{inventoryId}/invitations/{invitation.Id}", response);
    }

    [HttpGet]
    [Authorize(Policy = InventoryPolicies.Admin)]
    public async Task<IActionResult> ListForInventory(Guid inventoryId)
    {
        var invitations = await db.Invitations
            .Where(i => i.InventoryId == inventoryId)
            .Include(i => i.Inventory)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvitationResponse(
                i.Id,
                i.InventoryId,
                i.Inventory.Name,
                i.InvitedUserId,
                i.InvitedByUserId,
                i.Role.ToString(),
                i.Status.ToString(),
                i.CreatedAt))
            .ToArrayAsync();

        return Ok(invitations);
    }

    [HttpGet("/api/invitations")]
    [Authorize]
    public async Task<IActionResult> Mine()
    {
        var invitations = await db.Invitations
            .Where(i => i.InvitedUserId == currentUser.Id && i.Status == InvitationStatus.Pending)
            .Include(i => i.Inventory)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvitationResponse(
                i.Id,
                i.InventoryId,
                i.Inventory.Name,
                i.InvitedUserId,
                i.InvitedByUserId,
                i.Role.ToString(),
                i.Status.ToString(),
                i.CreatedAt))
            .ToArrayAsync();

        return Ok(invitations);
    }

    [HttpPost("/api/invitations/{id:guid}/accept")]
    [Authorize]
    public async Task<IActionResult> Accept(Guid id)
    {
        var invitation = await db.Invitations.FindAsync(id);
        if (invitation is null)
        {
            return ApiProblem.NotFound("INVITATION_NOT_FOUND", "Invitation not found.");
        }

        if (invitation.InvitedUserId != currentUser.Id)
        {
            return ApiProblem.Forbidden("NOT_YOUR_INVITATION", "This invitation is not addressed to you.");
        }

        if (invitation.Status != InvitationStatus.Pending)
        {
            return ApiProblem.Conflict("INVITATION_NOT_PENDING", "This invitation is no longer pending.");
        }

        invitation.Status = InvitationStatus.Accepted;
        invitation.RespondedAt = DateTimeOffset.UtcNow;

        db.Memberships.Add(new Membership
        {
            Id = Guid.CreateVersion7(),
            InventoryId = invitation.InventoryId,
            UserId = currentUser.Id,
            Role = invitation.Role,
            JoinedAt = DateTimeOffset.UtcNow,
        });

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            return ApiProblem.Conflict("INVITATION_NOT_PENDING", "This invitation is no longer pending.");
        }

        return Ok();
    }

    [HttpPost("/api/invitations/{id:guid}/decline")]
    [Authorize]
    public async Task<IActionResult> Decline(Guid id)
    {
        var invitation = await db.Invitations.FindAsync(id);
        if (invitation is null)
        {
            return ApiProblem.NotFound("INVITATION_NOT_FOUND", "Invitation not found.");
        }

        if (invitation.InvitedUserId != currentUser.Id)
        {
            return ApiProblem.Forbidden("NOT_YOUR_INVITATION", "This invitation is not addressed to you.");
        }

        if (invitation.Status != InvitationStatus.Pending)
        {
            return ApiProblem.Conflict("INVITATION_NOT_PENDING", "This invitation is no longer pending.");
        }

        invitation.Status = InvitationStatus.Declined;
        invitation.RespondedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("/api/invitations/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Revoke(Guid id)
    {
        var invitation = await db.Invitations.FindAsync(id);
        if (invitation is null)
        {
            return ApiProblem.NotFound("INVITATION_NOT_FOUND", "Invitation not found.");
        }

        var role = await roleHandler.ResolveRoleAsync(invitation.InventoryId, currentUser.Id);
        if (role is not (MembershipRole.Admin or MembershipRole.Owner))
        {
            return ApiProblem.Forbidden("NOT_INVENTORY_ADMIN", "You must be an admin or owner of this inventory.");
        }

        if (invitation.Status != InvitationStatus.Pending)
        {
            return ApiProblem.Conflict("INVITATION_NOT_PENDING", "This invitation is no longer pending.");
        }

        invitation.Status = InvitationStatus.Revoked;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
