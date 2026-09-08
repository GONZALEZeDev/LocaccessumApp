using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Locaccessum.Api.Contracts.Equipment;
using Locaccessum.Api.Contracts.Inventories;
using Locaccessum.Domain.Entities;
using Locaccessum.Domain.Enums;
using Locaccessum.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Locaccessum.Tests.Api;

[Collection("db")]
public class InventorySettingsTests(PostgresFixture fx) : IntegrationTest(fx)
{
    [Fact]
    public async Task Admin_can_patch_name_but_not_delete()
    {
        var (_, ownerToken) = await Register("owner-a@x.io");
        var invId = await CreateInventory(ownerToken, "Original");
        var (_, adminToken) = await AddMemberAsync(invId, "admin-a@x.io", MembershipRole.Admin);
        var adminClient = await AuthedClient(adminToken);

        var patch = await adminClient.PatchAsJsonAsync($"/api/inventories/{invId}", new { name = "Renamed" });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await patch.Content.ReadFromJsonAsync<InventoryResponse>();
        body!.Name.Should().Be("Renamed");

        var del = await adminClient.DeleteAsync($"/api/inventories/{invId}");
        del.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Owner_can_delete()
    {
        var (_, ownerToken) = await Register("owner-b@x.io");
        var invId = await CreateInventory(ownerToken, "ToDelete");
        var ownerClient = await AuthedClient(ownerToken);

        var del = await ownerClient.DeleteAsync($"/api/inventories/{invId}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await ownerClient.GetAsync($"/api/inventories/{invId}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_cascades_through_equipment_and_reservations()
    {
        var (ownerId, ownerToken) = await Register("owner-cascade@x.io");
        var invId = await CreateInventory(ownerToken, "CascadeTeam");
        var ownerClient = await AuthedClient(ownerToken);
        var eq = await (await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Cascade", reference = "C1" })).Content.ReadFromJsonAsync<EquipmentResponse>();

        var reservationId = Guid.CreateVersion7();
        var now = DateTimeOffset.UtcNow;
        await using (var seedDb = Fixture.NewDbContext())
        {
            seedDb.Reservations.Add(new Reservation
            {
                Id = reservationId,
                EquipmentId = eq!.Id,
                UserId = ownerId,
                Status = ReservationStatus.Confirmed,
                StartsAt = now.AddDays(1),
                EndsAt = now.AddDays(1).AddHours(2),
                CreatedAt = now,
            });
            await seedDb.SaveChangesAsync();
        }

        var del = await ownerClient.DeleteAsync($"/api/inventories/{invId}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var verifyDb = Fixture.NewDbContext();
        (await verifyDb.Equipment.AnyAsync(e => e.Id == eq!.Id)).Should().BeFalse();
        (await verifyDb.Reservations.AnyAsync(r => r.Id == reservationId)).Should().BeFalse();
    }

    [Fact]
    public async Task Transfer_swaps_roles_and_owner_id()
    {
        var (_, ownerToken) = await Register("owner-c@x.io");
        var invId = await CreateInventory(ownerToken, "ToTransfer");
        var (newOwnerId, newOwnerToken) = await AddMemberAsync(invId, "member-c@x.io", MembershipRole.Member);
        var ownerClient = await AuthedClient(ownerToken);

        var transfer = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/transfer-ownership", new { newOwnerUserId = newOwnerId });
        transfer.StatusCode.Should().Be(HttpStatusCode.OK);

        var newOwnerClient = await AuthedClient(newOwnerToken);
        var newOwnerView = await (await newOwnerClient.GetAsync($"/api/inventories/{invId}")).Content.ReadFromJsonAsync<InventoryResponse>();
        newOwnerView!.MyRole.Should().Be("Owner");
        newOwnerView!.OwnerId.Should().Be(newOwnerId);

        var oldOwnerView = await (await ownerClient.GetAsync($"/api/inventories/{invId}")).Content.ReadFromJsonAsync<InventoryResponse>();
        oldOwnerView!.MyRole.Should().Be("Admin");
        oldOwnerView!.OwnerId.Should().Be(newOwnerId);
    }

    [Fact]
    public async Task Transfer_to_non_member_is_400()
    {
        var (_, ownerToken) = await Register("owner-d@x.io");
        var invId = await CreateInventory(ownerToken, "NoTransfer");
        var (strangerId, _) = await Register("stranger-d@x.io");
        var ownerClient = await AuthedClient(ownerToken);

        var transfer = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/transfer-ownership", new { newOwnerUserId = strangerId });
        transfer.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await transfer.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("code").GetString().Should().Be("NOT_A_MEMBER");
    }

    [Fact]
    public async Task Delete_without_auth_is_401()
    {
        var (_, ownerToken) = await Register("owner-e@x.io");
        var invId = await CreateInventory(ownerToken, "NoAuthDelete");

        var del = await Client.DeleteAsync($"/api/inventories/{invId}");
        del.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
