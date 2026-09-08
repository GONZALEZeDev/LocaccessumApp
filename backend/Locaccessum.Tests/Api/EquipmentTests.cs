using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Locaccessum.Api.Contracts.Equipment;
using Locaccessum.Domain.Entities;
using Locaccessum.Domain.Enums;
using Locaccessum.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Locaccessum.Tests.Api;

[Collection("db")]
public class EquipmentTests(PostgresFixture fx) : IntegrationTest(fx)
{
    [Fact]
    public async Task Create_stores_informations_verbatim_and_defaults_status_active()
    {
        var (_, ownerToken) = await Register("owner-eq1@x.io");
        var invId = await CreateInventory(ownerToken, "EqTeam1");
        var ownerClient = await AuthedClient(ownerToken);

        var res = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment",
            new { name = "Projecteur", reference = "EPSON-EB", informations = "<b>220V</b>" });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<EquipmentResponse>();
        body!.Informations.Should().Be("<b>220V</b>");
        body.Status.Should().Be("Active");
    }

    [Fact]
    public async Task Member_cannot_create_equipment()
    {
        var (_, ownerToken) = await Register("owner-eq2@x.io");
        var invId = await CreateInventory(ownerToken, "EqTeam2");
        var (_, memberToken) = await AddMemberAsync(invId, "member-eq2@x.io", MembershipRole.Member);
        var memberClient = await AuthedClient(memberToken);

        var res = await memberClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment",
            new { name = "Perceuse", reference = "BOSCH-1" });
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Non_member_cannot_list_equipment()
    {
        var (_, ownerToken) = await Register("owner-eq9@x.io");
        var invId = await CreateInventory(ownerToken, "EqTeam9");
        var (_, strangerToken) = await Register("stranger-eq9@x.io");
        var strangerClient = await AuthedClient(strangerToken);

        var res = await strangerClient.GetAsync($"/api/inventories/{invId}/equipment");
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_equipment_by_id_as_member_succeeds()
    {
        var (_, ownerToken) = await Register("owner-eq3@x.io");
        var invId = await CreateInventory(ownerToken, "EqTeam3");
        var (_, memberToken) = await AddMemberAsync(invId, "member-eq3@x.io", MembershipRole.Member);
        var ownerClient = await AuthedClient(ownerToken);
        var create = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Camera", reference = "SONY-A7" });
        var eq = await create.Content.ReadFromJsonAsync<EquipmentResponse>();

        var memberClient = await AuthedClient(memberToken);
        var get = await memberClient.GetAsync($"/api/equipment/{eq!.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_equipment_by_id_as_non_member_is_403()
    {
        var (_, ownerToken) = await Register("owner-eq4@x.io");
        var invId = await CreateInventory(ownerToken, "EqTeam4");
        var ownerClient = await AuthedClient(ownerToken);
        var create = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Casque VR", reference = "META-Q3" });
        var eq = await create.Content.ReadFromJsonAsync<EquipmentResponse>();

        var (_, strangerToken) = await Register("stranger-eq4@x.io");
        var strangerClient = await AuthedClient(strangerToken);
        var get = await strangerClient.GetAsync($"/api/equipment/{eq!.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Patch_status_to_retired_succeeds()
    {
        var (_, ownerToken) = await Register("owner-eq5@x.io");
        var invId = await CreateInventory(ownerToken, "EqTeam5");
        var ownerClient = await AuthedClient(ownerToken);
        var create = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Fourgon", reference = "RENAULT-M" });
        var eq = await create.Content.ReadFromJsonAsync<EquipmentResponse>();

        var patch = await ownerClient.PatchAsJsonAsync($"/api/equipment/{eq!.Id}", new { status = "Retired" });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await patch.Content.ReadFromJsonAsync<EquipmentResponse>();
        updated!.Status.Should().Be("Retired");
    }

    [Fact]
    public async Task Non_admin_cannot_patch_equipment()
    {
        var (_, ownerToken) = await Register("owner-eq6@x.io");
        var invId = await CreateInventory(ownerToken, "EqTeam6");
        var (_, memberToken) = await AddMemberAsync(invId, "member-eq6@x.io", MembershipRole.Member);
        var ownerClient = await AuthedClient(ownerToken);
        var create = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Tablette", reference = "IPAD-10" });
        var eq = await create.Content.ReadFromJsonAsync<EquipmentResponse>();

        var memberClient = await AuthedClient(memberToken);
        var patch = await memberClient.PatchAsJsonAsync($"/api/equipment/{eq!.Id}", new { status = "Maintenance" });
        patch.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_with_reservation_is_409()
    {
        var (_, ownerToken) = await Register("owner-eq7@x.io");
        var invId = await CreateInventory(ownerToken, "EqTeam7");
        var ownerClient = await AuthedClient(ownerToken);
        var create = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Drone", reference = "DJI-M1" });
        var eq = await create.Content.ReadFromJsonAsync<EquipmentResponse>();

        // Seed a reservation directly (booking API doesn't exist until Tasks 18/19)
        await using (var seedDb = Fixture.NewDbContext())
        {
            seedDb.Reservations.Add(new Reservation
            {
                Id = Guid.CreateVersion7(),
                EquipmentId = eq!.Id,
                UserId = (await seedDb.Users.FirstAsync()).Id,
                StartsAt = DateTimeOffset.UtcNow.AddDays(1),
                EndsAt = DateTimeOffset.UtcNow.AddDays(1).AddHours(2),
                Status = ReservationStatus.Confirmed,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await seedDb.SaveChangesAsync();
        }

        var delete = await ownerClient.DeleteAsync($"/api/equipment/{eq!.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await delete.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().Should().Be("EQUIPMENT_HAS_RESERVATIONS");
    }

    [Fact]
    public async Task Delete_without_reservation_succeeds()
    {
        var (_, ownerToken) = await Register("owner-eq8@x.io");
        var invId = await CreateInventory(ownerToken, "EqTeam8");
        var ownerClient = await AuthedClient(ownerToken);
        var create = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Micro", reference = "SHURE-SM7" });
        var eq = await create.Content.ReadFromJsonAsync<EquipmentResponse>();

        var delete = await ownerClient.DeleteAsync($"/api/equipment/{eq!.Id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var get = await ownerClient.GetAsync($"/api/equipment/{eq!.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
