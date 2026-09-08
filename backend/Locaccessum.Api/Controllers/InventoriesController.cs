using Locaccessum.Api.Abstractions;
using Locaccessum.Api.Authorization;
using Locaccessum.Api.Common;
using Locaccessum.Api.Contracts.Inventories;
using Locaccessum.Domain.Entities;
using Locaccessum.Domain.Enums;
using Locaccessum.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Locaccessum.Api.Controllers;

[ApiController]
[Route("api/inventories")]
[Authorize]
public class InventoriesController(LocaccessumDbContext db, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateInventoryRequest req)
    {
        var inventory = new Inventory
        {
            Id = Guid.CreateVersion7(),
            Name = req.Name.Trim(),
            Description = req.Description?.Trim(),
            OwnerId = currentUser.Id,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        var membership = new Membership
        {
            Id = Guid.CreateVersion7(),
            InventoryId = inventory.Id,
            UserId = currentUser.Id,
            Role = MembershipRole.Owner,
            JoinedAt = DateTimeOffset.UtcNow,
        };

        db.Add(inventory);
        db.Add(membership);
        await db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, new InventoryResponse(
            inventory.Id, inventory.Name, inventory.Description, inventory.OwnerId, inventory.CreatedAt, MembershipRole.Owner.ToString()));
    }

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var items = await db.Memberships
            .Where(m => m.UserId == currentUser.Id)
            .Select(m => new InventoryListItemResponse(
                m.Inventory.Id,
                m.Inventory.Name,
                m.Inventory.Description,
                m.Role.ToString(),
                db.Memberships.Count(x => x.InventoryId == m.InventoryId)))
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id)
    {
        var membership = await db.Memberships
            .Include(m => m.Inventory)
            .SingleOrDefaultAsync(m => m.InventoryId == id && m.UserId == currentUser.Id);

        if (membership is null)
        {
            return ApiProblem.NotFound("INVENTORY_NOT_FOUND", "Inventory not found.");
        }

        var inventory = membership.Inventory;

        return Ok(new InventoryResponse(
            inventory.Id, inventory.Name, inventory.Description, inventory.OwnerId, inventory.CreatedAt, membership.Role.ToString()));
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Policy = InventoryPolicies.Admin)]
    public async Task<IActionResult> Patch(Guid id, UpdateInventoryRequest req)
    {
        var inventory = await db.Inventories.FindAsync(id);
        if (inventory is null)
        {
            return ApiProblem.NotFound("INVENTORY_NOT_FOUND", "Inventory not found.");
        }

        if (req.Name is not null)
        {
            inventory.Name = req.Name.Trim();
        }

        if (req.Description is not null)
        {
            inventory.Description = req.Description.Trim();
        }

        await db.SaveChangesAsync();

        var membership = await db.Memberships.SingleAsync(m => m.InventoryId == id && m.UserId == currentUser.Id);

        return Ok(new InventoryResponse(
            inventory.Id, inventory.Name, inventory.Description, inventory.OwnerId, inventory.CreatedAt, membership.Role.ToString()));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = InventoryPolicies.Owner)]
    public async Task<IActionResult> Delete(Guid id)
    {
        var inventory = await db.Inventories.FindAsync(id);
        if (inventory is null)
        {
            return ApiProblem.NotFound("INVENTORY_NOT_FOUND", "Inventory not found.");
        }

        db.Inventories.Remove(inventory);
        await db.SaveChangesAsync();

        return NoContent();
    }

    [HttpPost("{id:guid}/transfer-ownership")]
    [Authorize(Policy = InventoryPolicies.Owner)]
    public async Task<IActionResult> TransferOwnership(Guid id, TransferOwnershipRequest req)
    {
        var inventory = await db.Inventories.FindAsync(id);
        if (inventory is null)
        {
            return ApiProblem.NotFound("INVENTORY_NOT_FOUND", "Inventory not found.");
        }

        var currentOwnerMembership = await db.Memberships.SingleAsync(m => m.InventoryId == id && m.UserId == currentUser.Id);
        var targetMembership = await db.Memberships.SingleOrDefaultAsync(m => m.InventoryId == id && m.UserId == req.NewOwnerUserId);

        if (targetMembership is null)
        {
            return ApiProblem.Validation("NOT_A_MEMBER", "The target user is not a member of this inventory.");
        }

        currentOwnerMembership.Role = MembershipRole.Admin;
        targetMembership.Role = MembershipRole.Owner;
        inventory.OwnerId = req.NewOwnerUserId;

        await db.SaveChangesAsync();

        return Ok();
    }
}
