using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Locaccessum.Api.Contracts.Equipment;
using Locaccessum.Api.Contracts.Reservations;
using Locaccessum.Domain.Entities;
using Locaccessum.Domain.Enums;
using Locaccessum.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Locaccessum.Tests.Api;

[Collection("db")]
public class ReservationBookingTests(PostgresFixture fx) : IntegrationTest(fx)
{
    private static async Task<Guid> CreateEquipmentAsync(HttpClient client, Guid inventoryId, string name, string reference)
    {
        var res = await client.PostAsJsonAsync($"/api/inventories/{inventoryId}/equipment", new { name, reference });
        var body = await res.Content.ReadFromJsonAsync<EquipmentResponse>();
        return body!.Id;
    }

    [Fact]
    public async Task Books_a_free_unit_of_the_stack_and_returns_its_id()
    {
        var (_, ownerToken) = await Register("owner-rb1@x.io");
        var invId = await CreateInventory(ownerToken, "RbTeam1");
        var ownerClient = await AuthedClient(ownerToken);
        var unit1 = await CreateEquipmentAsync(ownerClient, invId, "Cam", "A");
        var unit2 = await CreateEquipmentAsync(ownerClient, invId, "Cam", "A");

        var start = DateTimeOffset.UtcNow.AddDays(1);
        var res = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations",
            new { name = "Cam", reference = "A", startsAt = start, endsAt = start.AddHours(2) });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await res.Content.ReadFromJsonAsync<ReservationResponse>();
        new[] { unit1, unit2 }.Should().Contain(body!.EquipmentId);
    }

    [Fact]
    public async Task Second_overlapping_booking_takes_the_other_unit()
    {
        var (_, ownerToken) = await Register("owner-rb2@x.io");
        var invId = await CreateInventory(ownerToken, "RbTeam2");
        var ownerClient = await AuthedClient(ownerToken);
        await CreateEquipmentAsync(ownerClient, invId, "Cam", "B");
        await CreateEquipmentAsync(ownerClient, invId, "Cam", "B");

        var start = DateTimeOffset.UtcNow.AddDays(2);
        var res1 = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations",
            new { name = "Cam", reference = "B", startsAt = start, endsAt = start.AddHours(2) });
        res1.StatusCode.Should().Be(HttpStatusCode.Created);
        var body1 = await res1.Content.ReadFromJsonAsync<ReservationResponse>();

        var res2 = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations",
            new { name = "Cam", reference = "B", startsAt = start.AddHours(1), endsAt = start.AddHours(3) });
        res2.StatusCode.Should().Be(HttpStatusCode.Created);
        var body2 = await res2.Content.ReadFromJsonAsync<ReservationResponse>();

        body2!.EquipmentId.Should().NotBe(body1!.EquipmentId);
    }

    [Fact]
    public async Task When_all_units_busy_returns_409_STACK_FULL_with_counts()
    {
        var (_, ownerToken) = await Register("owner-rb3@x.io");
        var invId = await CreateInventory(ownerToken, "RbTeam3");
        var ownerClient = await AuthedClient(ownerToken);
        await CreateEquipmentAsync(ownerClient, invId, "Cam", "C");
        await CreateEquipmentAsync(ownerClient, invId, "Cam", "C");

        var start = DateTimeOffset.UtcNow.AddDays(3);

        // Fill both units for a wide window.
        var r1 = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations",
            new { name = "Cam", reference = "C", startsAt = start, endsAt = start.AddHours(4) });
        r1.StatusCode.Should().Be(HttpStatusCode.Created);
        var r2 = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations",
            new { name = "Cam", reference = "C", startsAt = start, endsAt = start.AddHours(4) });
        r2.StatusCode.Should().Be(HttpStatusCode.Created);

        // Third booking overlapping the same range: no free unit left.
        var r3 = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations",
            new { name = "Cam", reference = "C", startsAt = start.AddHours(1), endsAt = start.AddHours(2) });
        r3.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var body = await r3.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().Should().Be("STACK_FULL");
        body.GetProperty("unitsTotal").GetInt32().Should().Be(2);
        body.GetProperty("unitsBusy").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Touching_intervals_do_not_conflict()
    {
        var (_, ownerToken) = await Register("owner-rb4@x.io");
        var invId = await CreateInventory(ownerToken, "RbTeam4");
        var ownerClient = await AuthedClient(ownerToken);
        await CreateEquipmentAsync(ownerClient, invId, "Cam", "D");

        var start = DateTimeOffset.UtcNow.AddDays(4);
        var res1 = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations",
            new { name = "Cam", reference = "D", startsAt = start, endsAt = start.AddHours(1) });
        res1.StatusCode.Should().Be(HttpStatusCode.Created);
        var body1 = await res1.Content.ReadFromJsonAsync<ReservationResponse>();

        var res2 = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations",
            new { name = "Cam", reference = "D", startsAt = start.AddHours(1), endsAt = start.AddHours(2) });
        res2.StatusCode.Should().Be(HttpStatusCode.Created);
        var body2 = await res2.Content.ReadFromJsonAsync<ReservationResponse>();

        body2!.EquipmentId.Should().Be(body1!.EquipmentId);
    }

    [Fact]
    public async Task Past_start_is_400_PAST_START()
    {
        var (_, ownerToken) = await Register("owner-rb5@x.io");
        var invId = await CreateInventory(ownerToken, "RbTeam5");
        var ownerClient = await AuthedClient(ownerToken);
        await CreateEquipmentAsync(ownerClient, invId, "Cam", "E");

        var start = DateTimeOffset.UtcNow.AddDays(-1);
        var res = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations",
            new { name = "Cam", reference = "E", startsAt = start, endsAt = start.AddHours(1) });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().Should().Be("PAST_START");
    }

    [Fact]
    public async Task Time_order_startsAt_after_endsAt_is_400_TIME_ORDER()
    {
        var (_, ownerToken) = await Register("owner-rb6@x.io");
        var invId = await CreateInventory(ownerToken, "RbTeam6");
        var ownerClient = await AuthedClient(ownerToken);
        await CreateEquipmentAsync(ownerClient, invId, "Cam", "F");

        var start = DateTimeOffset.UtcNow.AddDays(6);
        var res = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations",
            new { name = "Cam", reference = "F", startsAt = start, endsAt = start.AddHours(-1) });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().Should().Be("TIME_ORDER");
    }

    [Fact]
    public async Task Specific_equipmentId_that_is_retired_is_400_UNIT_UNAVAILABLE()
    {
        var (_, ownerToken) = await Register("owner-rb7@x.io");
        var invId = await CreateInventory(ownerToken, "RbTeam7");
        var ownerClient = await AuthedClient(ownerToken);
        var unitId = await CreateEquipmentAsync(ownerClient, invId, "Cam", "G");

        var patch = await ownerClient.PatchAsJsonAsync($"/api/equipment/{unitId}", new { status = "Retired" });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var start = DateTimeOffset.UtcNow.AddDays(7);
        var res = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations",
            new { equipmentId = unitId, startsAt = start, endsAt = start.AddHours(1) });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().Should().Be("UNIT_UNAVAILABLE");
    }

    [Fact]
    public async Task Specific_equipmentId_nonexistent_is_404()
    {
        var (_, ownerToken) = await Register("owner-rb8@x.io");
        var invId = await CreateInventory(ownerToken, "RbTeam8");
        var ownerClient = await AuthedClient(ownerToken);

        var start = DateTimeOffset.UtcNow.AddDays(8);
        var res = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/reservations",
            new { equipmentId = Guid.NewGuid(), startsAt = start, endsAt = start.AddHours(1) });

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Exclusion_constraint_rejects_overlapping_confirmed_rows_inserted_directly()
    {
        var (ownerId, ownerToken) = await Register("owner-excl@x.io");
        var invId = await CreateInventory(ownerToken, "ExclTeam");
        var ownerClient = await AuthedClient(ownerToken);
        var eq = await (await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Excl", reference = "E1" })).Content.ReadFromJsonAsync<EquipmentResponse>();

        var start = DateTimeOffset.UtcNow.AddDays(1);
        await using (var db = Fixture.NewDbContext())
        {
            db.Reservations.Add(new Reservation { Id = Guid.CreateVersion7(), EquipmentId = eq!.Id, UserId = ownerId, Status = ReservationStatus.Confirmed, StartsAt = start, EndsAt = start.AddHours(2), CreatedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }

        await using var db2 = Fixture.NewDbContext();
        db2.Reservations.Add(new Reservation { Id = Guid.CreateVersion7(), EquipmentId = eq!.Id, UserId = ownerId, Status = ReservationStatus.Confirmed, StartsAt = start.AddHours(1), EndsAt = start.AddHours(3), CreatedAt = DateTimeOffset.UtcNow });
        var act = async () => await db2.SaveChangesAsync();
        var ex = await act.Should().ThrowAsync<DbUpdateException>();
        (ex.And.InnerException as PostgresException)?.SqlState.Should().Be("23P01");
    }
}
