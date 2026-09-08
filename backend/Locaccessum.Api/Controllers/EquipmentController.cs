using Locaccessum.Api.Abstractions;
using Locaccessum.Api.Authorization;
using Locaccessum.Api.Common;
using Locaccessum.Api.Contracts.Equipment;
using Locaccessum.Domain.Enums;
using Locaccessum.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Locaccessum.Api.Controllers;

[ApiController]
public class EquipmentController(LocaccessumDbContext db, ICurrentUser currentUser, InventoryRoleHandler roleHandler) : ControllerBase
{
    [HttpPost]
    [Route("api/inventories/{inventoryId:guid}/equipment")]
    [Authorize(Policy = InventoryPolicies.Admin)]
    public async Task<IActionResult> Create(Guid inventoryId, CreateEquipmentRequest req)
    {
        var status = EquipmentStatus.Active;
        if (req.Status is not null)
        {
            if (!Enum.TryParse<EquipmentStatus>(req.Status, ignoreCase: true, out status) || !Enum.IsDefined(status))
            {
                return ApiProblem.Validation("INVALID_STATUS", "Status must be Active, Maintenance, or Retired.");
            }
        }

        var equipment = new Domain.Entities.Equipment
        {
            Id = Guid.CreateVersion7(),
            InventoryId = inventoryId,
            Name = req.Name.Trim(),
            Reference = req.Reference.Trim(),
            Informations = req.Informations,
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Equipment.Add(equipment);
        await db.SaveChangesAsync();

        var response = new EquipmentResponse(
            equipment.Id,
            equipment.InventoryId,
            equipment.Name,
            equipment.Reference,
            equipment.Informations,
            equipment.Status.ToString(),
            equipment.CreatedAt);

        return Created($"/api/equipment/{equipment.Id}", response);
    }

    [HttpGet]
    [Route("api/inventories/{inventoryId:guid}/equipment")]
    [Authorize(Policy = InventoryPolicies.Member)]
    public async Task<IActionResult> ListForInventory(Guid inventoryId, [FromQuery] bool grouped = false)
    {
        if (grouped)
        {
            var equipmentList = await db.Equipment
                .Where(e => e.InventoryId == inventoryId)
                .AsNoTracking()
                .ToListAsync();

            var stacks = equipmentList
                .GroupBy(e => (e.Name, e.Reference))
                .Select(g => new EquipmentStackResponse(
                    g.Key.Name,
                    g.Key.Reference,
                    g.Count(),
                    g.Count(e => e.Status == EquipmentStatus.Active),
                    g.Count(e => e.Status == EquipmentStatus.Maintenance),
                    g.Count(e => e.Status == EquipmentStatus.Retired),
                    g.OrderBy(e => e.CreatedAt).Select(e => e.Id).ToArray()))
                .OrderBy(s => s.Name)
                .ThenBy(s => s.Reference)
                .ToList();

            return Ok(stacks);
        }

        var equipment = await db.Equipment
            .Where(e => e.InventoryId == inventoryId)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new EquipmentResponse(
                e.Id,
                e.InventoryId,
                e.Name,
                e.Reference,
                e.Informations,
                e.Status.ToString(),
                e.CreatedAt))
            .ToArrayAsync();

        return Ok(equipment);
    }

    [HttpGet("/api/equipment/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Get(Guid id)
    {
        var equipment = await db.Equipment.FindAsync(id);
        if (equipment is null)
        {
            return ApiProblem.NotFound("EQUIPMENT_NOT_FOUND", "Equipment not found.");
        }

        var role = await roleHandler.ResolveRoleAsync(equipment.InventoryId, currentUser.Id);
        if (role is null)
        {
            return ApiProblem.Forbidden("NOT_INVENTORY_MEMBER", "You are not a member of this inventory.");
        }

        return Ok(new EquipmentResponse(
            equipment.Id,
            equipment.InventoryId,
            equipment.Name,
            equipment.Reference,
            equipment.Informations,
            equipment.Status.ToString(),
            equipment.CreatedAt));
    }

    [HttpPatch("/api/equipment/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Patch(Guid id, UpdateEquipmentRequest req)
    {
        var equipment = await db.Equipment.FindAsync(id);
        if (equipment is null)
        {
            return ApiProblem.NotFound("EQUIPMENT_NOT_FOUND", "Equipment not found.");
        }

        var role = await roleHandler.ResolveRoleAsync(equipment.InventoryId, currentUser.Id);
        if (role is not (MembershipRole.Admin or MembershipRole.Owner))
        {
            return ApiProblem.Forbidden("NOT_INVENTORY_ADMIN", "You must be an admin or owner of this inventory.");
        }

        if (req.Name is not null)
        {
            equipment.Name = req.Name.Trim();
        }

        if (req.Reference is not null)
        {
            equipment.Reference = req.Reference.Trim();
        }

        if (req.Informations is not null)
        {
            equipment.Informations = req.Informations;
        }

        if (req.Status is not null)
        {
            if (!Enum.TryParse<EquipmentStatus>(req.Status, ignoreCase: true, out var status) || !Enum.IsDefined(status))
            {
                return ApiProblem.Validation("INVALID_STATUS", "Status must be Active, Maintenance, or Retired.");
            }

            equipment.Status = status;
        }

        await db.SaveChangesAsync();

        return Ok(new EquipmentResponse(
            equipment.Id,
            equipment.InventoryId,
            equipment.Name,
            equipment.Reference,
            equipment.Informations,
            equipment.Status.ToString(),
            equipment.CreatedAt));
    }

    [HttpDelete("/api/equipment/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        var equipment = await db.Equipment.FindAsync(id);
        if (equipment is null)
        {
            return ApiProblem.NotFound("EQUIPMENT_NOT_FOUND", "Equipment not found.");
        }

        var role = await roleHandler.ResolveRoleAsync(equipment.InventoryId, currentUser.Id);
        if (role is not (MembershipRole.Admin or MembershipRole.Owner))
        {
            return ApiProblem.Forbidden("NOT_INVENTORY_ADMIN", "You must be an admin or owner of this inventory.");
        }

        if (await db.Reservations.AnyAsync(r => r.EquipmentId == id))
        {
            return ApiProblem.Conflict("EQUIPMENT_HAS_RESERVATIONS", "This equipment has reservations; set status to Retired instead of deleting.");
        }

        db.Equipment.Remove(equipment);
        await db.SaveChangesAsync();

        return NoContent();
    }
}
