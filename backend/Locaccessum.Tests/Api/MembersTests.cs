using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Locaccessum.Api.Contracts.Members;
using Locaccessum.Domain.Entities;
using Locaccessum.Domain.Enums;
using Locaccessum.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Locaccessum.Tests.Api;

[Collection("db")]
public class MembersTests(PostgresFixture fx) : IntegrationTest(fx)
{
    [Fact]
    public async Task List_shows_all_members()
    {
        var (ownerId, ownerToken) = await Register("owner-m1@x.io");
        var invId = await CreateInventory(ownerToken, "Inv1");
        var (memberId, _) = await AddMemberAsync(invId, "m1@x.io", MembershipRole.Member);
        var ownerClient = await AuthedClient(ownerToken);

        var response = await ownerClient.GetAsync($"/api/inventories/{invId}/members");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var members = await response.Content.ReadFromJsonAsync<List<MemberResponse>>();
        members.Should().HaveCount(2);
        members.Should().ContainSingle(m => m.UserId == ownerId && m.Role == "Owner");
        members.Should().ContainSingle(m => m.UserId == memberId && m.Role == "Member");
    }

    [Fact]
    public async Task Admin_can_promote_member_to_admin()
    {
        var (_, ownerToken) = await Register("owner-m2@x.io");
        var invId = await CreateInventory(ownerToken, "Inv2");
        var (_, adminToken) = await AddMemberAsync(invId, "admin1@x.io", MembershipRole.Admin);
        var (memberId, _) = await AddMemberAsync(invId, "member1@x.io", MembershipRole.Member);
        var adminClient = await AuthedClient(adminToken);

        var patch = await adminClient.PatchAsJsonAsync($"/api/inventories/{invId}/members/{memberId}", new { role = "Admin" });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await (await adminClient.GetAsync($"/api/inventories/{invId}/members")).Content.ReadFromJsonAsync<List<MemberResponse>>();
        list.Should().ContainSingle(m => m.UserId == memberId && m.Role == "Admin");
    }

    [Fact]
    public async Task Cannot_patch_owner_role()
    {
        var (ownerId, ownerToken) = await Register("owner-m3@x.io");
        var invId = await CreateInventory(ownerToken, "Inv3");
        var ownerClient = await AuthedClient(ownerToken);

        var patch = await ownerClient.PatchAsJsonAsync($"/api/inventories/{invId}/members/{ownerId}", new { role = "Admin" });
        patch.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await patch.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("code").GetString().Should().Be("CANNOT_MODIFY_OWNER");
    }

    [Fact]
    public async Task Cannot_remove_owner()
    {
        var (ownerId, ownerToken) = await Register("owner-m4@x.io");
        var invId = await CreateInventory(ownerToken, "Inv4");
        var ownerClient = await AuthedClient(ownerToken);

        var del = await ownerClient.DeleteAsync($"/api/inventories/{invId}/members/{ownerId}");
        del.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await del.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("code").GetString().Should().Be("CANNOT_REMOVE_OWNER");
    }

    [Fact]
    public async Task Removing_member_cancels_their_upcoming_reservations()
    {
        var (ownerId, ownerToken) = await Register("owner-m5@x.io");
        var invId = await CreateInventory(ownerToken, "Inv5");
        var (memberId, _) = await AddMemberAsync(invId, "toremove@x.io", MembershipRole.Member);

        var now = DateTimeOffset.UtcNow;
        var equipmentId = Guid.CreateVersion7();
        var reservationId = Guid.CreateVersion7();

        await using (var seedDb = Fixture.NewDbContext())
        {
            seedDb.Add(new Equipment
            {
                Id = equipmentId,
                InventoryId = invId,
                Name = "Drone",
                Reference = "X1",
                Status = EquipmentStatus.Active,
                CreatedAt = now,
            });
            seedDb.Add(new Reservation
            {
                Id = reservationId,
                EquipmentId = equipmentId,
                UserId = memberId,
                Status = ReservationStatus.Confirmed,
                StartsAt = now.AddHours(1),
                EndsAt = now.AddHours(3),
                CreatedAt = now,
            });
            await seedDb.SaveChangesAsync();
        }

        var ownerClient = await AuthedClient(ownerToken);
        var del = await ownerClient.DeleteAsync($"/api/inventories/{invId}/members/{memberId}");
        del.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using var verifyDb = Fixture.NewDbContext();
        var reservation = await verifyDb.Reservations.SingleAsync(r => r.Id == reservationId);
        reservation.Status.Should().Be(ReservationStatus.Cancelled);
        reservation.CancelledAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Non_member_cannot_list_members()
    {
        var (_, ownerToken) = await Register("owner-m7@x.io");
        var invId = await CreateInventory(ownerToken, "Inv7");
        var (_, strangerToken) = await Register("stranger-m7@x.io");
        var strangerClient = await AuthedClient(strangerToken);

        var response = await strangerClient.GetAsync($"/api/inventories/{invId}/members");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rejects_undefined_numeric_enum_value()
    {
        var (_, ownerToken) = await Register("owner-m6@x.io");
        var invId = await CreateInventory(ownerToken, "Inv6");
        var (memberId, _) = await AddMemberAsync(invId, "member6@x.io", MembershipRole.Member);
        var ownerClient = await AuthedClient(ownerToken);

        var patch = await ownerClient.PatchAsJsonAsync($"/api/inventories/{invId}/members/{memberId}", new { role = "42" });
        patch.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await patch.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        body.GetProperty("code").GetString().Should().Be("INVALID_ROLE");
    }
}
