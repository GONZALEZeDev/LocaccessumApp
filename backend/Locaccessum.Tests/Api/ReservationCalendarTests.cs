using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Locaccessum.Api.Contracts.Equipment;
using Locaccessum.Api.Contracts.Reservations;
using Locaccessum.Domain.Entities;
using Locaccessum.Domain.Enums;
using Locaccessum.Tests.Infrastructure;

namespace Locaccessum.Tests.Api;

[Collection("db")]
public class ReservationCalendarTests(PostgresFixture fx) : IntegrationTest(fx)
{
    [Fact]
    public async Task Calendar_returns_overlapping_confirmed_excludes_outside_window_and_cancelled()
    {
        var (ownerId, ownerToken) = await Register("owner-cal1@x.io");
        var invId = await CreateInventory(ownerToken, "CalTeam1");
        var ownerClient = await AuthedClient(ownerToken);
        var eqRes = await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Camera", reference = "SONY-A7" });
        var eq = await eqRes.Content.ReadFromJsonAsync<EquipmentResponse>();

        var windowStart = DateTimeOffset.UtcNow.AddDays(10);
        var windowEnd = windowStart.AddDays(2);

        Guid insideId, straddlingId;
        await using (var db = Fixture.NewDbContext())
        {
            var inside = new Reservation { Id = Guid.CreateVersion7(), EquipmentId = eq!.Id, UserId = ownerId, Status = ReservationStatus.Confirmed, StartsAt = windowStart.AddHours(2), EndsAt = windowStart.AddHours(4), CreatedAt = DateTimeOffset.UtcNow };
            var straddling = new Reservation { Id = Guid.CreateVersion7(), EquipmentId = eq.Id, UserId = ownerId, Status = ReservationStatus.Confirmed, StartsAt = windowStart.AddHours(-1), EndsAt = windowStart.AddHours(1), CreatedAt = DateTimeOffset.UtcNow };
            var outside = new Reservation { Id = Guid.CreateVersion7(), EquipmentId = eq.Id, UserId = ownerId, Status = ReservationStatus.Confirmed, StartsAt = windowEnd.AddDays(1), EndsAt = windowEnd.AddDays(1).AddHours(2), CreatedAt = DateTimeOffset.UtcNow };
            var cancelled = new Reservation { Id = Guid.CreateVersion7(), EquipmentId = eq.Id, UserId = ownerId, Status = ReservationStatus.Cancelled, StartsAt = windowStart.AddHours(2), EndsAt = windowStart.AddHours(4), CreatedAt = DateTimeOffset.UtcNow, CancelledAt = DateTimeOffset.UtcNow };
            db.AddRange(inside, straddling, outside, cancelled);
            await db.SaveChangesAsync();
            insideId = inside.Id; straddlingId = straddling.Id;
        }

        var url = $"/api/inventories/{invId}/reservations?from={Uri.EscapeDataString(windowStart.ToString("O"))}&to={Uri.EscapeDataString(windowEnd.ToString("O"))}";
        var res = await ownerClient.GetAsync(url);
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await res.Content.ReadFromJsonAsync<List<ReservationResponse>>();
        items!.Select(i => i.Id).Should().BeEquivalentTo([insideId, straddlingId]);
    }

    [Fact]
    public async Task Calendar_equipmentId_filter_narrows_results()
    {
        var (ownerId, ownerToken) = await Register("owner-cal2@x.io");
        var invId = await CreateInventory(ownerToken, "CalTeam2");
        var ownerClient = await AuthedClient(ownerToken);
        var eq1 = await (await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Drone", reference = "DJI-1" })).Content.ReadFromJsonAsync<EquipmentResponse>();
        var eq2 = await (await ownerClient.PostAsJsonAsync($"/api/inventories/{invId}/equipment", new { name = "Drone", reference = "DJI-2" })).Content.ReadFromJsonAsync<EquipmentResponse>();

        var start = DateTimeOffset.UtcNow.AddDays(20);
        var end = start.AddHours(4);
        Guid r1Id;
        await using (var db = Fixture.NewDbContext())
        {
            var r1 = new Reservation { Id = Guid.CreateVersion7(), EquipmentId = eq1!.Id, UserId = ownerId, Status = ReservationStatus.Confirmed, StartsAt = start.AddHours(1), EndsAt = start.AddHours(2), CreatedAt = DateTimeOffset.UtcNow };
            var r2 = new Reservation { Id = Guid.CreateVersion7(), EquipmentId = eq2!.Id, UserId = ownerId, Status = ReservationStatus.Confirmed, StartsAt = start.AddHours(1), EndsAt = start.AddHours(2), CreatedAt = DateTimeOffset.UtcNow };
            db.AddRange(r1, r2);
            await db.SaveChangesAsync();
            r1Id = r1.Id;
        }

        var url = $"/api/inventories/{invId}/reservations?from={Uri.EscapeDataString(start.ToString("O"))}&to={Uri.EscapeDataString(end.ToString("O"))}&equipmentId={eq1!.Id}";
        var res = await ownerClient.GetAsync(url);
        var items = await res.Content.ReadFromJsonAsync<List<ReservationResponse>>();
        items!.Select(i => i.Id).Should().BeEquivalentTo([r1Id]);
    }

    [Fact]
    public async Task Non_member_cannot_view_calendar()
    {
        var (_, ownerToken) = await Register("owner-cal4@x.io");
        var invId = await CreateInventory(ownerToken, "CalTeam4");
        var (_, strangerToken) = await Register("stranger-cal4@x.io");
        var strangerClient = await AuthedClient(strangerToken);
        var start = DateTimeOffset.UtcNow.AddDays(1);

        var url = $"/api/inventories/{invId}/reservations?from={Uri.EscapeDataString(start.ToString("O"))}&to={Uri.EscapeDataString(start.AddHours(1).ToString("O"))}";
        var res = await strangerClient.GetAsync(url);
        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Calendar_from_after_to_is_400()
    {
        var (_, ownerToken) = await Register("owner-cal3@x.io");
        var invId = await CreateInventory(ownerToken, "CalTeam3");
        var ownerClient = await AuthedClient(ownerToken);
        var start = DateTimeOffset.UtcNow.AddDays(1);
        var url = $"/api/inventories/{invId}/reservations?from={Uri.EscapeDataString(start.ToString("O"))}&to={Uri.EscapeDataString(start.AddHours(-1).ToString("O"))}";
        var res = await ownerClient.GetAsync(url);
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
