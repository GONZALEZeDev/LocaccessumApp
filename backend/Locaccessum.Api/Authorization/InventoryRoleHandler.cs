using System.Security.Claims;
using Locaccessum.Domain.Enums;
using Locaccessum.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Locaccessum.Api.Authorization;

/// <summary>
/// Authorizes requests against a caller's <see cref="Domain.Entities.Membership"/> role on the
/// inventory named by the route's <c>id</c> or <c>inventoryId</c> segment. Ranks roles explicitly
/// (see <see cref="Rank"/>) rather than comparing <see cref="MembershipRole"/> values directly,
/// because the enum's declaration order (Owner, Admin, Member) is the reverse of privilege order.
/// </summary>
public class InventoryRoleHandler(LocaccessumDbContext db, IHttpContextAccessor accessor)
    : AuthorizationHandler<InventoryRoleRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, InventoryRoleRequirement requirement)
    {
        var routeValues = accessor.HttpContext!.Request.RouteValues;
        var idValue = routeValues.TryGetValue("id", out var idObj)
            ? idObj
            : routeValues.TryGetValue("inventoryId", out var invIdObj) ? invIdObj : null;

        if (idValue is not string idStr || !Guid.TryParse(idStr, out var inventoryId))
        {
            return;
        }

        var subClaim = context.User.FindFirstValue("sub");
        if (subClaim is null || !Guid.TryParse(subClaim, out var userId))
        {
            return;
        }

        var role = await ResolveRoleAsync(inventoryId, userId);

        if (role is not null && Rank(role.Value) >= Rank(requirement.Minimum))
        {
            context.Succeed(requirement);
        }
    }

    public async Task<MembershipRole?> ResolveRoleAsync(Guid inventoryId, Guid userId)
    {
        var m = await db.Memberships.AsNoTracking()
            .SingleOrDefaultAsync(x => x.InventoryId == inventoryId && x.UserId == userId);
        return m?.Role;
    }

    static int Rank(MembershipRole role) => role switch
    {
        MembershipRole.Owner => 3,
        MembershipRole.Admin => 2,
        MembershipRole.Member => 1,
        _ => 0,
    };
}
